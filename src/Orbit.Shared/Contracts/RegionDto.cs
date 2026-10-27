namespace Orbit.Shared.Contracts;

/// <summary>A provider region with the geo-coordinates Orbit uses to place it on the globe.</summary>
public sealed record RegionDto
{
    public string Slug { get; init; } = "";
    public string Name { get; init; } = "";
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public bool Available { get; init; }
    public int DropletCount { get; init; }
}
