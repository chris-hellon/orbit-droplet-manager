using Microsoft.EntityFrameworkCore;
using Orbit.Core.Data;
using Orbit.Core.Providers;
using Orbit.Core.Security;
using Orbit.Shared;
using Orbit.Shared.Contracts;

namespace Orbit.Core.Services;

/// <summary>Raised when an operation needs a provider token but none is stored.</summary>
public sealed class NotConnectedException() : Exception("No provider connection is configured. Add an API token first.");

public interface IConnectionService
{
    Task<ConnectionStatusDto> GetStatusAsync(CancellationToken ct = default);
    Task<ProviderAccountDto> ValidateAsync(string token, ProviderType provider, CancellationToken ct = default);
    Task<ConnectionStatusDto> ConnectAsync(ConnectRequest request, CancellationToken ct = default);
    Task DisconnectAsync(CancellationToken ct = default);

    /// <summary>The decrypted token for the given provider, or throws <see cref="NotConnectedException"/>.</summary>
    Task<string> GetTokenAsync(ProviderType provider, CancellationToken ct = default);

    Task<ProviderConnection?> GetConnectionAsync(ProviderType provider, CancellationToken ct = default);
}

public sealed class ConnectionService(
    OrbitDbContext db,
    ITokenProtector protector,
    IProviderResolver providers,
    TimeProvider clock,
    IActivityRecorder activity) : IConnectionService
{
    private const ProviderType DefaultProvider = ProviderType.DigitalOcean;

    public async Task<ConnectionStatusDto> GetStatusAsync(CancellationToken ct = default)
    {
        var conn = await GetConnectionAsync(DefaultProvider, ct);
        if (conn is null) return ConnectionStatusDto.Disconnected;

        return new ConnectionStatusDto
        {
            Connected = true,
            Provider = conn.Provider,
            Label = conn.Label,
            TokenHint = "••••" + conn.TokenLast4,
            AccountEmail = conn.AccountEmail,
            DropletLimit = conn.DropletLimit,
            LastValidatedAt = conn.LastValidatedAt,
        };
    }

    public Task<ProviderAccountDto> ValidateAsync(string token, ProviderType provider, CancellationToken ct = default)
        => providers.Get(provider).ValidateTokenAsync(token.Trim(), ct);

    public async Task<ConnectionStatusDto> ConnectAsync(ConnectRequest request, CancellationToken ct = default)
    {
        var token = request.Token.Trim();
        var account = await providers.Get(request.Provider).ValidateTokenAsync(token, ct);

        var conn = await db.Connections
            .FirstOrDefaultAsync(c => c.WorkspaceId == Seed.WorkspaceId && c.Provider == request.Provider, ct);

        var now = clock.GetUtcNow();
        if (conn is null)
        {
            conn = new ProviderConnection
            {
                Id = Guid.NewGuid(),
                WorkspaceId = Seed.WorkspaceId,
                Provider = request.Provider,
                CreatedAt = now,
            };
            db.Connections.Add(conn);
        }

        conn.EncryptedToken = protector.Protect(token);
        conn.TokenLast4 = token.Length >= 4 ? token[^4..] : token;
        conn.Label = string.IsNullOrWhiteSpace(request.Label) ? account.Email : request.Label!.Trim();
        conn.AccountEmail = account.Email;
        conn.DropletLimit = account.DropletLimit;
        conn.LastValidatedAt = now;

        await db.SaveChangesAsync(ct);
        await activity.RecordAsync("connection", $"Connected to DigitalOcean ({account.Email})", kind: "success", ct: ct);

        return await GetStatusAsync(ct);
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        await db.Connections
            .Where(c => c.WorkspaceId == Seed.WorkspaceId)
            .ExecuteDeleteAsync(ct);
        await activity.RecordAsync("connection", "Disconnected from DigitalOcean", kind: "info", ct: ct);
    }

    public async Task<string> GetTokenAsync(ProviderType provider, CancellationToken ct = default)
    {
        var conn = await GetConnectionAsync(provider, ct)
            ?? throw new NotConnectedException();
        return protector.Unprotect(conn.EncryptedToken);
    }

    public Task<ProviderConnection?> GetConnectionAsync(ProviderType provider, CancellationToken ct = default)
        => db.Connections.AsNoTracking()
            .FirstOrDefaultAsync(c => c.WorkspaceId == Seed.WorkspaceId && c.Provider == provider, ct);
}
