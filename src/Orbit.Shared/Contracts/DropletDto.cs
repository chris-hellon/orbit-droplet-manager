namespace Orbit.Shared.Contracts;

/// <summary>
/// A single droplet as the UI needs it: provider facts (specs, networking, region) plus
/// derived/live fields (health, latest metrics) merged in by the server.
/// </summary>
public sealed record DropletDto
{
    public long Id { get; init; }
    public string Name { get; init; } = "";

    /// <summary>Raw provider status: active, off, new, archive.</summary>
    public string Status { get; init; } = "";

    public DropletHealth Health { get; init; } = DropletHealth.Unknown;

    public string RegionSlug { get; init; } = "";
    public string RegionName { get; init; } = "";
    public double Latitude { get; init; }
    public double Longitude { get; init; }

    public string SizeSlug { get; init; } = "";
    public int Vcpus { get; init; }
    public int MemoryMb { get; init; }
    public int DiskGb { get; init; }
    public decimal PriceMonthly { get; init; }

    public string? PublicIpv4 { get; init; }
    public string? PrivateIpv4 { get; init; }
    public string? PublicIpv6 { get; init; }

    public string ImageDistribution { get; init; } = "";
    public string ImageName { get; init; } = "";

    public IReadOnlyList<string> Tags { get; init; } = [];
    public IReadOnlyList<string> Features { get; init; } = [];

    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>True while a provider action (reboot, snapshot, ...) is running — drives the pulse animation.</summary>
    public bool ActionInProgress { get; init; }

    // ---- Live metrics (null until the poller has a reading; requires the DO monitoring agent) ----
    public double? CpuPercent { get; init; }
    public double? MemoryPercent { get; init; }
    public double? DiskPercent { get; init; }
    public DateTimeOffset? MetricsAt { get; init; }

    /// <summary>Earliest expiring TLS certificate found for this droplet, if any (SSL warnings).</summary>
    public DateTimeOffset? SslExpiresAt { get; init; }

    public bool IsPoweredOn => string.Equals(Status, "active", StringComparison.OrdinalIgnoreCase);
}
