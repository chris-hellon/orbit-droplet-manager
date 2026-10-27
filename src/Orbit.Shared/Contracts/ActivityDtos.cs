namespace Orbit.Shared.Contracts;

/// <summary>A single entry in the dashboard activity feed.</summary>
public sealed record ActivityEntryDto
{
    public long Id { get; init; }
    public DateTimeOffset TimestampUtc { get; init; }
    public string Category { get; init; } = "";
    public long? DropletId { get; init; }
    public string Message { get; init; } = "";
    public string Kind { get; init; } = "info"; // info | success | warning
}
