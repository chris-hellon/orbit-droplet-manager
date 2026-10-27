using Orbit.Core.Providers;
using Orbit.Shared;
using Orbit.Shared.Contracts;

namespace Orbit.Core.Services;

public interface IDatabaseService
{
    Task<IReadOnlyList<DatabaseClusterDto>> ListAsync(CancellationToken ct = default);
    Task<DatabaseClusterDto?> GetAsync(string clusterId, CancellationToken ct = default);
    Task<IReadOnlyList<DatabaseUserDto>> ListUsersAsync(string clusterId, CancellationToken ct = default);
    Task<DatabaseUserDto> CreateUserAsync(string clusterId, string name, CancellationToken ct = default);
    Task DeleteUserAsync(string clusterId, string name, CancellationToken ct = default);
    Task<IReadOnlyList<DatabaseDbDto>> ListDatabasesAsync(string clusterId, CancellationToken ct = default);
    Task<DatabaseDbDto> CreateDatabaseAsync(string clusterId, string name, CancellationToken ct = default);
    Task DeleteDatabaseAsync(string clusterId, string name, CancellationToken ct = default);
}

public sealed class DatabaseService(IConnectionService connections, IDatabaseProvider databases) : IDatabaseService
{
    private async Task<string> TokenAsync(CancellationToken ct) => await connections.GetTokenAsync(ProviderType.DigitalOcean, ct);

    public async Task<IReadOnlyList<DatabaseClusterDto>> ListAsync(CancellationToken ct = default)
        => await databases.ListClustersAsync(await TokenAsync(ct), ct);

    public async Task<DatabaseClusterDto?> GetAsync(string clusterId, CancellationToken ct = default)
        => await databases.GetClusterAsync(await TokenAsync(ct), clusterId, ct);

    public async Task<IReadOnlyList<DatabaseUserDto>> ListUsersAsync(string clusterId, CancellationToken ct = default)
        => await databases.ListUsersAsync(await TokenAsync(ct), clusterId, ct);

    public async Task<DatabaseUserDto> CreateUserAsync(string clusterId, string name, CancellationToken ct = default)
        => await databases.CreateUserAsync(await TokenAsync(ct), clusterId, name, ct);

    public async Task DeleteUserAsync(string clusterId, string name, CancellationToken ct = default)
        => await databases.DeleteUserAsync(await TokenAsync(ct), clusterId, name, ct);

    public async Task<IReadOnlyList<DatabaseDbDto>> ListDatabasesAsync(string clusterId, CancellationToken ct = default)
        => await databases.ListDatabasesAsync(await TokenAsync(ct), clusterId, ct);

    public async Task<DatabaseDbDto> CreateDatabaseAsync(string clusterId, string name, CancellationToken ct = default)
        => await databases.CreateDatabaseAsync(await TokenAsync(ct), clusterId, name, ct);

    public async Task DeleteDatabaseAsync(string clusterId, string name, CancellationToken ct = default)
        => await databases.DeleteDatabaseAsync(await TokenAsync(ct), clusterId, name, ct);
}
