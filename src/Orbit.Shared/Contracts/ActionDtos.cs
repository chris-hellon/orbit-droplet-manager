using System.ComponentModel.DataAnnotations;

namespace Orbit.Shared.Contracts;

/// <summary>Request a power operation against a droplet.</summary>
public sealed class PowerActionRequest
{
    public PowerAction Action { get; set; }
}

/// <summary>Request a snapshot of a droplet.</summary>
public sealed class SnapshotRequest
{
    [Required(ErrorMessage = "A snapshot name is required.")]
    [MinLength(1)]
    public string Name { get; set; } = "";
}

/// <summary>The provider action that was queued, so the UI can reflect "in progress".</summary>
public sealed record DropletActionDto
{
    public long Id { get; init; }
    public long DropletId { get; init; }
    public string Type { get; init; } = "";
    public string Status { get; init; } = "";
    public DateTimeOffset StartedAt { get; init; }
}
