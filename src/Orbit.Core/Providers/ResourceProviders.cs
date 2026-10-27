using Orbit.Shared.Contracts;

namespace Orbit.Core.Providers;

/// <summary>DNS domains + records at the provider. DigitalOcean today; stateless (token per call).</summary>
public interface IDnsProvider
{
    Task<IReadOnlyList<DomainDto>> ListDomainsAsync(string token, CancellationToken ct = default);
    Task<DomainDto> CreateDomainAsync(string token, CreateDomainRequest request, CancellationToken ct = default);
    Task DeleteDomainAsync(string token, string name, CancellationToken ct = default);

    Task<IReadOnlyList<DnsRecordDto>> ListRecordsAsync(string token, string domain, CancellationToken ct = default);
    Task<DnsRecordDto> CreateRecordAsync(string token, string domain, DnsRecordRequest request, CancellationToken ct = default);
    Task<DnsRecordDto> UpdateRecordAsync(string token, string domain, long recordId, DnsRecordRequest request, CancellationToken ct = default);
    Task DeleteRecordAsync(string token, string domain, long recordId, CancellationToken ct = default);
}

/// <summary>Managed database clusters, their users and logical databases.</summary>
public interface IDatabaseProvider
{
    Task<IReadOnlyList<DatabaseClusterDto>> ListClustersAsync(string token, CancellationToken ct = default);
    Task<DatabaseClusterDto?> GetClusterAsync(string token, string clusterId, CancellationToken ct = default);

    Task<IReadOnlyList<DatabaseUserDto>> ListUsersAsync(string token, string clusterId, CancellationToken ct = default);
    Task<DatabaseUserDto> CreateUserAsync(string token, string clusterId, string name, CancellationToken ct = default);
    Task DeleteUserAsync(string token, string clusterId, string name, CancellationToken ct = default);

    Task<IReadOnlyList<DatabaseDbDto>> ListDatabasesAsync(string token, string clusterId, CancellationToken ct = default);
    Task<DatabaseDbDto> CreateDatabaseAsync(string token, string clusterId, string name, CancellationToken ct = default);
    Task DeleteDatabaseAsync(string token, string clusterId, string name, CancellationToken ct = default);
}
