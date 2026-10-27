using Microsoft.Extensions.Logging;
using Orbit.Shared;
using Orbit.Shared.Contracts;

namespace Orbit.Core.Providers.DigitalOcean;

/// <summary>
/// DigitalOcean implementation of <see cref="IDropletProvider"/>. Droplet list/specs/metrics/actions
/// against api.digitalocean.com/v2. Shares HTTP plumbing via <see cref="DigitalOceanApi"/>.
/// </summary>
public sealed class DigitalOceanProvider(DigitalOceanApi api, ILogger<DigitalOceanProvider> logger)
    : IDropletProvider
{
    public ProviderType Provider => ProviderType.DigitalOcean;

    public async Task<ProviderAccountDto> ValidateTokenAsync(string token, CancellationToken ct = default)
    {
        var res = await api.GetAsync<DoAccountResponse>(token, "/v2/account", ct);
        var a = res.Account;
        return new ProviderAccountDto { Email = a.Email, Uuid = a.Uuid, DropletLimit = a.DropletLimit, Status = a.Status };
    }

    public async Task<IReadOnlyList<DropletDto>> ListDropletsAsync(string token, CancellationToken ct = default)
    {
        var droplets = await api.ListAllAsync<DoDropletsResponse, DoDroplet>(
            token, "/v2/droplets?per_page=200", r => r.Droplets, r => r.Links?.Pages?.Next, ct);
        return droplets.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<RegionDto>> ListRegionsAsync(string token, CancellationToken ct = default)
    {
        var res = await api.GetAsync<DoRegionsResponse>(token, "/v2/regions?per_page=200", ct);
        return res.Regions.Select(r =>
        {
            var geo = DigitalOceanRegions.Resolve(r.Slug);
            return new RegionDto { Slug = r.Slug, Name = r.Name, Latitude = geo.Latitude, Longitude = geo.Longitude, Available = r.Available };
        }).ToList();
    }

    public async Task<DropletMetricsDto> GetMetricsAsync(string token, long dropletId, CancellationToken ct = default)
    {
        var end = DateTimeOffset.UtcNow;
        var start = end.AddMinutes(-5);

        var cpuTask = GetMetricAsync(token, "cpu", dropletId, start, end, ct);
        var memTotalTask = GetMetricAsync(token, "memory_total", dropletId, start, end, ct);
        var memAvailTask = GetMetricAsync(token, "memory_available", dropletId, start, end, ct);
        var fsSizeTask = GetMetricAsync(token, "filesystem_size", dropletId, start, end, ct);
        var fsFreeTask = GetMetricAsync(token, "filesystem_free", dropletId, start, end, ct);

        await Task.WhenAll(cpuTask, memTotalTask, memAvailTask, fsSizeTask, fsFreeTask);

        var cpu = ComputeCpuPercent(cpuTask.Result);
        var memPercent = ComputeUsedPercent(SumLatest(memTotalTask.Result), SumLatest(memAvailTask.Result));
        var diskPercent = ComputeUsedPercent(SumLatest(fsSizeTask.Result), SumLatest(fsFreeTask.Result));
        var noData = cpu is null && memPercent is null && diskPercent is null;

        return new DropletMetricsDto
        {
            DropletId = dropletId,
            CpuPercent = cpu,
            MemoryPercent = memPercent,
            DiskPercent = diskPercent,
            TimestampUtc = end,
            NoData = noData,
            Health = HealthEvaluator.Evaluate("active", cpu, memPercent, diskPercent),
        };
    }

    public Task<DropletActionDto> PowerActionAsync(string token, long dropletId, PowerAction action, CancellationToken ct = default)
    {
        var type = action switch
        {
            PowerAction.PowerOn => "power_on",
            PowerAction.PowerOff => "power_off",
            PowerAction.Reboot => "reboot",
            PowerAction.Shutdown => "shutdown",
            PowerAction.PowerCycle => "power_cycle",
            _ => throw new ProviderException($"Unknown power action '{action}'."),
        };
        return PostActionAsync(token, dropletId, new { type }, ct);
    }

    public Task<DropletActionDto> SnapshotAsync(string token, long dropletId, string name, CancellationToken ct = default)
        => PostActionAsync(token, dropletId, new { type = "snapshot", name }, ct);

    public async Task<IReadOnlyList<SizeDto>> ListSizesAsync(string token, CancellationToken ct = default)
    {
        var sizes = await api.ListAllAsync<DoSizesResponse, DoSizeFull>(
            token, "/v2/sizes?per_page=200", r => r.Sizes, r => r.Links?.Pages?.Next, ct);
        return sizes.Select(s => new SizeDto
        {
            Slug = s.Slug,
            Vcpus = s.Vcpus,
            MemoryMb = s.Memory,
            DiskGb = s.Disk,
            PriceMonthly = s.PriceMonthly,
            Available = s.Available,
            Regions = s.Regions ?? [],
        }).ToList();
    }

    public async Task<IReadOnlyList<ImageDto>> ListImagesAsync(string token, CancellationToken ct = default)
    {
        // Base distributions + the user's own snapshots/backups.
        var distros = await api.ListAllAsync<DoImagesResponse, DoImageFull>(
            token, "/v2/images?type=distribution&per_page=200", r => r.Images, r => r.Links?.Pages?.Next, ct);
        var priv = await api.ListAllAsync<DoImagesResponse, DoImageFull>(
            token, "/v2/images?private=true&per_page=200", r => r.Images, r => r.Links?.Pages?.Next, ct);

        return distros.Concat(priv).Select(i => new ImageDto
        {
            Id = i.Id,
            Slug = i.Slug,
            Name = i.Name ?? "",
            Distribution = i.Distribution ?? "",
            Type = i.Type ?? "",
            CreatedAt = i.CreatedAt,
        }).ToList();
    }

    public Task<DropletActionDto> ResizeAsync(string token, long dropletId, string size, bool resizeDisk, CancellationToken ct = default)
        => PostActionAsync(token, dropletId, new { type = "resize", size, disk = resizeDisk }, ct);

    public Task<DropletActionDto> RebuildAsync(string token, long dropletId, string image, CancellationToken ct = default)
        => PostActionAsync(token, dropletId, new { type = "rebuild", image }, ct);

    // ---------- helpers ----------

    private async Task<DropletActionDto> PostActionAsync(string token, long dropletId, object body, CancellationToken ct)
    {
        var res = await api.PostAsync<DoActionResponse>(token, $"/v2/droplets/{dropletId}/actions", body, ct);
        var a = res.Action;
        return new DropletActionDto { Id = a.Id, DropletId = dropletId, Type = a.Type, Status = a.Status, StartedAt = a.StartedAt };
    }

    private static DropletDto Map(DoDroplet d)
    {
        var geo = DigitalOceanRegions.Resolve(d.Region?.Slug ?? "");
        var v4 = d.Networks?.V4 ?? [];
        var v6 = d.Networks?.V6 ?? [];

        return new DropletDto
        {
            Id = d.Id,
            Name = d.Name,
            Status = d.Status,
            RegionSlug = d.Region?.Slug ?? "",
            RegionName = d.Region?.Name ?? geo.City,
            Latitude = geo.Latitude,
            Longitude = geo.Longitude,
            SizeSlug = d.SizeSlug,
            Vcpus = d.Vcpus,
            MemoryMb = d.Memory,
            DiskGb = d.Disk,
            PriceMonthly = d.Size?.PriceMonthly ?? 0m,
            PublicIpv4 = v4.FirstOrDefault(n => n.Type == "public")?.IpAddress,
            PrivateIpv4 = v4.FirstOrDefault(n => n.Type == "private")?.IpAddress,
            PublicIpv6 = v6.FirstOrDefault(n => n.Type == "public")?.IpAddress,
            ImageDistribution = d.Image?.Distribution ?? "",
            ImageName = d.Image?.Name ?? "",
            Tags = d.Tags ?? [],
            Features = d.Features ?? [],
            CreatedAt = d.CreatedAt,
            Health = DropletHealth.Unknown,
        };
    }

    private async Task<DoMetricsResponse?> GetMetricAsync(
        string token, string metric, long dropletId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct)
    {
        var url = $"/v2/monitoring/metrics/droplet/{metric}?host_id={dropletId}&start={start.ToUnixTimeSeconds()}&end={end.ToUnixTimeSeconds()}";
        try
        {
            return await api.GetAsync<DoMetricsResponse>(token, url, ct);
        }
        catch (ProviderException ex)
        {
            logger.LogDebug("Metric '{Metric}' unavailable for droplet {Id}: {Message}", metric, dropletId, ex.Message);
            return null;
        }
    }

    private static double? ComputeCpuPercent(DoMetricsResponse? res)
    {
        var series = res?.Data?.Result;
        if (series is null || series.Count == 0) return null;

        double totalDelta = 0, idleDelta = 0;
        var any = false;
        foreach (var s in series)
        {
            var fl = FirstLast(s);
            if (fl is null) continue;
            var delta = Math.Max(0, fl.Value.last - fl.Value.first);
            totalDelta += delta;
            if (s.Metric is not null && s.Metric.TryGetValue("mode", out var mode) && mode.Equals("idle", StringComparison.OrdinalIgnoreCase))
                idleDelta += delta;
            any = true;
        }
        if (!any || totalDelta <= 0) return null;
        return Math.Clamp((totalDelta - idleDelta) / totalDelta * 100, 0, 100);
    }

    private static double? ComputeUsedPercent(double? total, double? free)
    {
        if (total is null or <= 0 || free is null) return null;
        return Math.Clamp((total.Value - free.Value) / total.Value * 100, 0, 100);
    }

    private static double? SumLatest(DoMetricsResponse? res)
    {
        var series = res?.Data?.Result;
        if (series is null || series.Count == 0) return null;
        double sum = 0;
        var any = false;
        foreach (var s in series)
        {
            var fl = FirstLast(s);
            if (fl is null) continue;
            sum += fl.Value.last;
            any = true;
        }
        return any ? sum : null;
    }

    private static (double first, double last)? FirstLast(DoMetricsSeries s)
    {
        if (s.Values is null || s.Values.Count == 0) return null;
        var first = ParsePair(s.Values[0]);
        var last = ParsePair(s.Values[^1]);
        if (first is null || last is null) return null;
        return (first.Value, last.Value);

        static double? ParsePair(System.Text.Json.JsonElement pair)
        {
            if (pair.ValueKind != System.Text.Json.JsonValueKind.Array) return null;
            var arr = pair.EnumerateArray().ToArray();
            if (arr.Length < 2) return null;
            var str = arr[1].ValueKind == System.Text.Json.JsonValueKind.String ? arr[1].GetString() : arr[1].GetRawText();
            return DigitalOceanApi.ParseMetricValue(str);
        }
    }
}
