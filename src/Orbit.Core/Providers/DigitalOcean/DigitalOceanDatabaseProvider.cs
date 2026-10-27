using Orbit.Core.Providers;
using Orbit.Shared.Contracts;

namespace Orbit.Core.Providers.DigitalOcean;

/// <summary>DigitalOcean managed databases via /v2/databases.</summary>
public sealed class DigitalOceanDatabaseProvider(DigitalOceanApi api) : IDatabaseProvider
{
    public async Task<IReadOnlyList<DatabaseClusterDto>> ListClustersAsync(string token, CancellationToken ct = default)
    {
        var res = await api.GetAsync<DoDatabasesResponse>(token, "/v2/databases", ct);
        return (res.Databases ?? []).Select(Map).ToList();
    }

    public async Task<DatabaseClusterDto?> GetClusterAsync(string token, string clusterId, CancellationToken ct = default)
    {
        try
        {
            var res = await api.GetAsync<DoDatabaseResponse>(token, $"/v2/databases/{clusterId}", ct);
            return Map(res.Database);
        }
        catch (ProviderException ex) when (ex.StatusCode == 404)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<DatabaseUserDto>> ListUsersAsync(string token, string clusterId, CancellationToken ct = default)
    {
        var res = await api.GetAsync<DoDbUsersResponse>(token, $"/v2/databases/{clusterId}/users", ct);
        return (res.Users ?? []).Select(Map).ToList();
    }

    public async Task<DatabaseUserDto> CreateUserAsync(string token, string clusterId, string name, CancellationToken ct = default)
    {
        var res = await api.PostAsync<DoDbUserResponse>(token, $"/v2/databases/{clusterId}/users", new { name = name.Trim() }, ct);
        return Map(res.User);
    }

    public Task DeleteUserAsync(string token, string clusterId, string name, CancellationToken ct = default)
        => api.SendNoContentAsync(token, HttpMethod.Delete, $"/v2/databases/{clusterId}/users/{Uri.EscapeDataString(name)}", ct: ct);

    public async Task<IReadOnlyList<DatabaseDbDto>> ListDatabasesAsync(string token, string clusterId, CancellationToken ct = default)
    {
        var res = await api.GetAsync<DoDbsResponse>(token, $"/v2/databases/{clusterId}/dbs", ct);
        return (res.Dbs ?? []).Select(d => new DatabaseDbDto { Name = d.Name }).ToList();
    }

    public async Task<DatabaseDbDto> CreateDatabaseAsync(string token, string clusterId, string name, CancellationToken ct = default)
    {
        var res = await api.PostAsync<DoDbResponse>(token, $"/v2/databases/{clusterId}/dbs", new { name = name.Trim() }, ct);
        return new DatabaseDbDto { Name = res.Db.Name };
    }

    public Task DeleteDatabaseAsync(string token, string clusterId, string name, CancellationToken ct = default)
        => api.SendNoContentAsync(token, HttpMethod.Delete, $"/v2/databases/{clusterId}/dbs/{Uri.EscapeDataString(name)}", ct: ct);

    private static DatabaseClusterDto Map(DoDatabase d) => new()
    {
        Id = d.Id,
        Name = d.Name,
        Engine = d.Engine,
        Version = d.Version ?? "",
        Status = d.Status,
        SizeSlug = d.Size,
        RegionSlug = d.Region,
        NumNodes = d.NumNodes,
        CreatedAt = d.CreatedAt,
        Tags = d.Tags ?? [],
        Connection = d.Connection is { } c
            ? new DbConnectionDto
            {
                Host = c.Host ?? "",
                Port = c.Port,
                User = c.User ?? "",
                Password = c.Password,
                Database = c.Database ?? "",
                Ssl = c.Ssl,
                Uri = c.Uri,
            }
            : null,
    };

    private static DatabaseUserDto Map(DoDbUser u) => new() { Name = u.Name, Role = u.Role, Password = u.Password };
}
