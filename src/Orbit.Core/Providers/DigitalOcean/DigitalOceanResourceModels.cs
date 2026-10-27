using System.Text.Json.Serialization;

namespace Orbit.Core.Providers.DigitalOcean;

// ---- Domains / DNS ----

internal sealed record DoDomainsResponse(
    [property: JsonPropertyName("domains")] List<DoDomain> Domains,
    [property: JsonPropertyName("links")] DoLinks? Links);

internal sealed record DoDomainResponse([property: JsonPropertyName("domain")] DoDomain Domain);

internal sealed record DoDomain(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("ttl")] int? Ttl);

internal sealed record DoRecordsResponse(
    [property: JsonPropertyName("domain_records")] List<DoDomainRecord> Records,
    [property: JsonPropertyName("links")] DoLinks? Links);

internal sealed record DoRecordResponse([property: JsonPropertyName("domain_record")] DoDomainRecord Record);

internal sealed record DoDomainRecord(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("data")] string Data,
    [property: JsonPropertyName("priority")] int? Priority,
    [property: JsonPropertyName("port")] int? Port,
    [property: JsonPropertyName("weight")] int? Weight,
    [property: JsonPropertyName("ttl")] int? Ttl,
    [property: JsonPropertyName("flags")] int? Flags,
    [property: JsonPropertyName("tag")] string? Tag);

// ---- Managed databases ----

internal sealed record DoDatabasesResponse([property: JsonPropertyName("databases")] List<DoDatabase>? Databases);

internal sealed record DoDatabaseResponse([property: JsonPropertyName("database")] DoDatabase Database);

internal sealed record DoDatabase(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("engine")] string Engine,
    [property: JsonPropertyName("version")] string? Version,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("size")] string Size,
    [property: JsonPropertyName("region")] string Region,
    [property: JsonPropertyName("num_nodes")] int NumNodes,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("connection")] DoDbConnection? Connection,
    [property: JsonPropertyName("tags")] List<string>? Tags);

internal sealed record DoDbConnection(
    [property: JsonPropertyName("host")] string? Host,
    [property: JsonPropertyName("port")] int Port,
    [property: JsonPropertyName("user")] string? User,
    [property: JsonPropertyName("password")] string? Password,
    [property: JsonPropertyName("database")] string? Database,
    [property: JsonPropertyName("ssl")] bool Ssl,
    [property: JsonPropertyName("uri")] string? Uri);

internal sealed record DoDbUsersResponse([property: JsonPropertyName("users")] List<DoDbUser>? Users);
internal sealed record DoDbUserResponse([property: JsonPropertyName("user")] DoDbUser User);
internal sealed record DoDbUser(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("role")] string? Role,
    [property: JsonPropertyName("password")] string? Password);

internal sealed record DoDbsResponse([property: JsonPropertyName("dbs")] List<DoDb>? Dbs);
internal sealed record DoDbResponse([property: JsonPropertyName("db")] DoDb Db);
internal sealed record DoDb([property: JsonPropertyName("name")] string Name);
