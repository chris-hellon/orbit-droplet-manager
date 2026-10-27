using Orbit.Core.Providers;
using Orbit.Shared;
using Orbit.Shared.Contracts;

namespace Orbit.Core.Services;

public interface IDropletService
{
    Task<IReadOnlyList<DropletDto>> GetDropletsAsync(CancellationToken ct = default);
    Task<DropletDto?> GetDropletAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<RegionDto>> GetRegionsAsync(CancellationToken ct = default);
    Task<DropletMetricsDto> RefreshMetricsAsync(long id, CancellationToken ct = default);
    Task<DropletActionDto> PowerAsync(long id, PowerAction action, CancellationToken ct = default);
    Task<DropletActionDto> SnapshotAsync(long id, string name, CancellationToken ct = default);
    Task<IReadOnlyList<SizeDto>> GetSizesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ImageDto>> GetImagesAsync(CancellationToken ct = default);
    Task<DropletActionDto> ResizeAsync(long id, string size, bool resizeDisk, CancellationToken ct = default);
    Task<DropletActionDto> RebuildAsync(long id, string image, CancellationToken ct = default);
}

/// <summary>
/// Orchestrates the active provider with the stored token and the in-memory state cache, producing
/// the enriched <see cref="DropletDto"/>s the UI consumes (specs + live metrics + action state).
/// </summary>
public sealed class DropletService(
    IConnectionService connections,
    IProviderResolver providers,
    DropletStateCache cache,
    IActivityRecorder activity) : IDropletService
{
    private const ProviderType DefaultProvider = ProviderType.DigitalOcean;

    public async Task<IReadOnlyList<DropletDto>> GetDropletsAsync(CancellationToken ct = default)
    {
        var (provider, token) = await ResolveAsync(ct);
        var droplets = await provider.ListDropletsAsync(token, ct);
        var enriched = droplets.Select(Enrich).ToList();
        cache.SetDropletIps(enriched);
        return enriched;
    }

    public async Task<DropletDto?> GetDropletAsync(long id, CancellationToken ct = default)
    {
        var droplets = await GetDropletsAsync(ct);
        return droplets.FirstOrDefault(d => d.Id == id);
    }

    public async Task<IReadOnlyList<RegionDto>> GetRegionsAsync(CancellationToken ct = default)
    {
        var (provider, token) = await ResolveAsync(ct);
        var regions = await provider.ListRegionsAsync(token, ct);

        // annotate with how many droplets currently sit in each region
        var droplets = await provider.ListDropletsAsync(token, ct);
        var counts = droplets.GroupBy(d => d.RegionSlug).ToDictionary(g => g.Key, g => g.Count());

        return regions
            .Select(r => r with { DropletCount = counts.GetValueOrDefault(r.Slug) })
            .ToList();
    }

    public async Task<DropletMetricsDto> RefreshMetricsAsync(long id, CancellationToken ct = default)
    {
        var (provider, token) = await ResolveAsync(ct);
        var metrics = await provider.GetMetricsAsync(token, id, ct);
        cache.UpsertMetrics(metrics);
        return metrics;
    }

    public async Task<DropletActionDto> PowerAsync(long id, PowerAction action, CancellationToken ct = default)
    {
        var (provider, token) = await ResolveAsync(ct);
        cache.MarkActionInProgress(id);
        try
        {
            var result = await provider.PowerActionAsync(token, id, action, ct);
            var verb = action switch
            {
                PowerAction.PowerOn => "Powered on",
                PowerAction.PowerOff => "Powered off",
                PowerAction.Reboot => "Rebooted",
                PowerAction.Shutdown => "Shut down",
                PowerAction.PowerCycle => "Power-cycled",
                _ => action.ToString(),
            };
            await activity.RecordAsync("power", verb, id, action == PowerAction.PowerOn ? "success" : "info", ct);
            return result;
        }
        catch
        {
            cache.ClearAction(id);
            throw;
        }
    }

    public async Task<DropletActionDto> SnapshotAsync(long id, string name, CancellationToken ct = default)
    {
        var (provider, token) = await ResolveAsync(ct);
        cache.MarkActionInProgress(id);
        try
        {
            var result = await provider.SnapshotAsync(token, id, name, ct);
            await activity.RecordAsync("snapshot", $"Snapshot “{name}” started", id, "info", ct);
            return result;
        }
        catch
        {
            cache.ClearAction(id);
            throw;
        }
    }

    public async Task<IReadOnlyList<SizeDto>> GetSizesAsync(CancellationToken ct = default)
    {
        var (provider, token) = await ResolveAsync(ct);
        return await provider.ListSizesAsync(token, ct);
    }

    public async Task<IReadOnlyList<ImageDto>> GetImagesAsync(CancellationToken ct = default)
    {
        var (provider, token) = await ResolveAsync(ct);
        return await provider.ListImagesAsync(token, ct);
    }

    public async Task<DropletActionDto> ResizeAsync(long id, string size, bool resizeDisk, CancellationToken ct = default)
    {
        var (provider, token) = await ResolveAsync(ct);
        cache.MarkActionInProgress(id);
        try
        {
            var result = await provider.ResizeAsync(token, id, size, resizeDisk, ct);
            await activity.RecordAsync("resize", $"Resize to {size} started", id, "info", ct);
            return result;
        }
        catch { cache.ClearAction(id); throw; }
    }

    public async Task<DropletActionDto> RebuildAsync(long id, string image, CancellationToken ct = default)
    {
        var (provider, token) = await ResolveAsync(ct);
        cache.MarkActionInProgress(id);
        try
        {
            var result = await provider.RebuildAsync(token, id, image, ct);
            await activity.RecordAsync("rebuild", $"Rebuild from {image} started", id, "warning", ct);
            return result;
        }
        catch { cache.ClearAction(id); throw; }
    }

    /// <summary>Overlay cached live metrics + action state onto a provider's raw droplet.</summary>
    public DropletDto Enrich(DropletDto d)
    {
        var m = cache.TryGetMetrics(d.Id);
        var cpu = m?.CpuPercent;
        var mem = m?.MemoryPercent;
        var disk = m?.DiskPercent;

        return d with
        {
            CpuPercent = cpu,
            MemoryPercent = mem,
            DiskPercent = disk,
            MetricsAt = m?.TimestampUtc,
            Health = HealthEvaluator.Evaluate(d.Status, cpu, mem, disk),
            ActionInProgress = cache.IsActionInProgress(d.Id),
        };
    }

    private async Task<(IDropletProvider provider, string token)> ResolveAsync(CancellationToken ct)
    {
        var token = await connections.GetTokenAsync(DefaultProvider, ct);
        return (providers.Get(DefaultProvider), token);
    }
}
