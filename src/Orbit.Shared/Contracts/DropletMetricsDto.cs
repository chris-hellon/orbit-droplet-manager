namespace Orbit.Shared.Contracts;

/// <summary>A point-in-time metrics reading for one droplet, pushed over SignalR and merged into <see cref="DropletDto"/>.</summary>
public sealed record DropletMetricsDto
{
    public long DropletId { get; init; }
    public double? CpuPercent { get; init; }
    public double? MemoryPercent { get; init; }
    public double? DiskPercent { get; init; }
    public DropletHealth Health { get; init; } = DropletHealth.Unknown;
    public DateTimeOffset TimestampUtc { get; init; }

    /// <summary>True when the provider returned no monitoring data (agent not installed / droppet off).</summary>
    public bool NoData { get; init; }
}
