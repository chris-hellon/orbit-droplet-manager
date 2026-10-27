namespace Orbit.Core;

/// <summary>
/// Root configuration, bound from the "Orbit" section (or ORBIT__ environment variables in Docker).
/// </summary>
public sealed class OrbitOptions
{
    public const string SectionName = "Orbit";

    /// <summary>Directory for the SQLite database and Data-Protection keys. Mount this as a volume in Docker.</summary>
    public string DataDirectory { get; set; } = "/data";

    /// <summary>SQLite database file name inside <see cref="DataDirectory"/>.</summary>
    public string DatabaseFileName { get; set; } = "orbit.db";

    /// <summary>Seconds between metric polls. DigitalOcean monitoring updates roughly every minute.</summary>
    public int MetricsPollSeconds { get; set; } = 60;

    /// <summary>Seconds between full droplet-list refreshes.</summary>
    public int DropletRefreshSeconds { get; set; } = 120;

    public string DatabasePath => Path.Combine(DataDirectory, DatabaseFileName);
    public string KeyRingPath => Path.Combine(DataDirectory, "keys");
}
