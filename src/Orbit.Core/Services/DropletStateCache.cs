using System.Collections.Concurrent;
using Orbit.Shared.Contracts;

namespace Orbit.Core.Services;

/// <summary>
/// Process-wide cache of the latest metrics reading per droplet and which droplets have an action
/// in progress. Populated by the poller / action calls, read when building <see cref="DropletDto"/>s.
/// </summary>
public sealed class DropletStateCache
{
    private readonly ConcurrentDictionary<long, DropletMetricsDto> _metrics = new();
    private readonly ConcurrentDictionary<long, DateTimeOffset> _actions = new();
    private readonly ConcurrentDictionary<long, string> _ips = new();

    /// <summary>Remember each droplet's public IPv4 so SSH management calls don't re-hit the provider API.</summary>
    public void SetDropletIps(IEnumerable<DropletDto> droplets)
    {
        foreach (var d in droplets)
            if (!string.IsNullOrWhiteSpace(d.PublicIpv4))
                _ips[d.Id] = d.PublicIpv4!;
    }

    public string? TryGetIp(long dropletId) => _ips.TryGetValue(dropletId, out var ip) ? ip : null;

    private static readonly TimeSpan ActionTtl = TimeSpan.FromMinutes(3);

    public void UpsertMetrics(DropletMetricsDto metrics) => _metrics[metrics.DropletId] = metrics;

    public DropletMetricsDto? TryGetMetrics(long dropletId)
        => _metrics.TryGetValue(dropletId, out var m) ? m : null;

    public void MarkActionInProgress(long dropletId) => _actions[dropletId] = DateTimeOffset.UtcNow;

    public void ClearAction(long dropletId) => _actions.TryRemove(dropletId, out _);

    public bool IsActionInProgress(long dropletId)
    {
        if (!_actions.TryGetValue(dropletId, out var startedAt)) return false;

        // Self-healing: an action flag that never got cleared expires so pillars don't pulse forever.
        if (DateTimeOffset.UtcNow - startedAt > ActionTtl)
        {
            _actions.TryRemove(dropletId, out _);
            return false;
        }
        return true;
    }
}
