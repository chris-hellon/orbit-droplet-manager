using System.ComponentModel.DataAnnotations;

namespace Orbit.Shared.Contracts;

/// <summary>A managed database cluster (PostgreSQL / MySQL / Redis / MongoDB / Kafka).</summary>
public sealed record DatabaseClusterDto
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Engine { get; init; } = "";
    public string Version { get; init; } = "";
    public string Status { get; init; } = "";
    public string SizeSlug { get; init; } = "";
    public string RegionSlug { get; init; } = "";
    public int NumNodes { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DbConnectionDto? Connection { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Health-ish mapping for the UI dot: "online" = good, "creating"/"migrating" = busy, else warn.</summary>
    public DropletHealth Health => Status switch
    {
        "online" => DropletHealth.Healthy,
        "creating" or "migrating" or "resizing" or "forking" => DropletHealth.Warning,
        "" => DropletHealth.Unknown,
        _ => DropletHealth.Critical,
    };
}

/// <summary>Connection details for a cluster. Password is the user's own secret, shown only to them.</summary>
public sealed record DbConnectionDto
{
    public string Host { get; init; } = "";
    public int Port { get; init; }
    public string User { get; init; } = "";
    public string? Password { get; init; }
    public string Database { get; init; } = "";
    public bool Ssl { get; init; }
    public string? Uri { get; init; }
}

public sealed record DatabaseUserDto
{
    public string Name { get; init; } = "";
    public string? Role { get; init; }
    public string? Password { get; init; }
}

public sealed record DatabaseDbDto
{
    public string Name { get; init; } = "";
}

public sealed class CreateDatabaseDbRequest
{
    [Required, MinLength(1)] public string Name { get; set; } = "";
}

public sealed class CreateDatabaseUserRequest
{
    [Required, MinLength(1)] public string Name { get; set; } = "";
}
