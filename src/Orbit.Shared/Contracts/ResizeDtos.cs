using System.ComponentModel.DataAnnotations;

namespace Orbit.Shared.Contracts;

/// <summary>A droplet size/plan available at the provider.</summary>
public sealed record SizeDto
{
    public string Slug { get; init; } = "";
    public int Vcpus { get; init; }
    public int MemoryMb { get; init; }
    public int DiskGb { get; init; }
    public decimal PriceMonthly { get; init; }
    public bool Available { get; init; }
    public IReadOnlyList<string> Regions { get; init; } = [];

    public string Specs => $"{Vcpus} vCPU · {MemoryMb / 1024.0:0.#} GB RAM · {DiskGb} GB";
    public string Label => $"{Slug} — {Specs} — ${PriceMonthly:0}/mo";
}

/// <summary>An image usable to rebuild a droplet (base distribution or a user snapshot/backup).</summary>
public sealed record ImageDto
{
    public long Id { get; init; }
    public string? Slug { get; init; }
    public string Name { get; init; } = "";
    public string Distribution { get; init; } = "";
    public string Type { get; init; } = "";   // distribution | snapshot | backup
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>What to send to the provider: slug for distributions, numeric id for snapshots.</summary>
    public string Reference => string.IsNullOrEmpty(Slug) ? Id.ToString() : Slug!;
    public bool IsSnapshot => Type is "snapshot" or "backup";
}

public sealed class ResizeRequest
{
    [Required] public string Size { get; set; } = "";
    /// <summary>Also permanently grow the disk (can't be undone; blocks future downsizing).</summary>
    public bool ResizeDisk { get; set; }
}

public sealed class RebuildRequest
{
    [Required] public string Image { get; set; } = "";
}
