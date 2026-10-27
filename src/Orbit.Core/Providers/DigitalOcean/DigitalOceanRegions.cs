namespace Orbit.Core.Providers.DigitalOcean;

/// <summary>
/// DigitalOcean's API does not return geo-coordinates for regions, so we map each datacenter slug
/// to the coordinates of its city. Used to place droplets on the globe.
/// </summary>
public static class DigitalOceanRegions
{
    public readonly record struct Geo(double Latitude, double Longitude, string City);

    private static readonly Dictionary<string, Geo> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["nyc"] = new(40.7128, -74.0060, "New York"),
        ["sfo"] = new(37.7749, -122.4194, "San Francisco"),
        ["ams"] = new(52.3676, 4.9041, "Amsterdam"),
        ["sgp"] = new(1.3521, 103.8198, "Singapore"),
        ["lon"] = new(51.5074, -0.1278, "London"),
        ["fra"] = new(50.1109, 8.6821, "Frankfurt"),
        ["tor"] = new(43.6532, -79.3832, "Toronto"),
        ["blr"] = new(12.9716, 77.5946, "Bangalore"),
        ["syd"] = new(-33.8688, 151.2093, "Sydney"),
        ["atl"] = new(33.7490, -84.3880, "Atlanta"),
    };

    /// <summary>Resolve a region slug (e.g. "nyc3") to coordinates. Falls back to (0,0) for unknown regions.</summary>
    public static Geo Resolve(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return new Geo(0, 0, "Unknown");

        // slugs are "<city><number>" — strip the trailing digits.
        var prefix = new string(slug.TakeWhile(char.IsLetter).ToArray());
        return Map.TryGetValue(prefix, out var geo) ? geo : new Geo(0, 0, slug);
    }
}
