using System.ComponentModel.DataAnnotations;

namespace Orbit.Shared.Contracts;

/// <summary>A DNS domain (zone) managed at the provider.</summary>
public sealed record DomainDto
{
    public string Name { get; init; } = "";
    public int Ttl { get; init; }
    public int RecordCount { get; init; }
}

/// <summary>A single DNS record within a domain.</summary>
public sealed record DnsRecordDto
{
    public long Id { get; init; }
    public string Type { get; init; } = "";
    public string Name { get; init; } = "";
    public string Data { get; init; } = "";
    public int? Priority { get; init; }
    public int? Port { get; init; }
    public int? Weight { get; init; }
    public int Ttl { get; init; }
    public int? Flags { get; init; }
    public string? Tag { get; init; }
}

public sealed class CreateDomainRequest
{
    [Required, MinLength(3)] public string Name { get; set; } = "";
    /// <summary>Optional A record for the apex, created with the domain.</summary>
    public string? IpAddress { get; set; }
}

/// <summary>Create or update a DNS record. Used for POST (create) and PUT (edit).</summary>
public sealed class DnsRecordRequest
{
    [Required] public string Type { get; set; } = "A";
    [Required] public string Name { get; set; } = "@";
    [Required] public string Data { get; set; } = "";
    public int? Priority { get; set; }
    public int? Port { get; set; }
    public int? Weight { get; set; }
    public int Ttl { get; set; } = 3600;
    public int? Flags { get; set; }
    public string? Tag { get; set; }
}
