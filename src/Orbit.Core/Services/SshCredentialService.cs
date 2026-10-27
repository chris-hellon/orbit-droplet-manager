using Microsoft.EntityFrameworkCore;
using Orbit.Core.Data;
using Orbit.Core.Security;
using Orbit.Shared.Contracts;

namespace Orbit.Core.Services;

/// <summary>Raised when an SSH operation is requested but no SSH key is configured.</summary>
public sealed class NoSshCredentialException() : Exception("No SSH key is configured. Add one in Settings to manage droplets.");

public sealed record DecryptedSshCredential(string PrivateKeyPem, string? Passphrase, string Username, int Port);

public interface ISshCredentialService
{
    Task<SshCredentialStatusDto> GetStatusAsync(CancellationToken ct = default);
    Task SaveAsync(SaveSshKeyRequest request, CancellationToken ct = default);
    Task DeleteAsync(CancellationToken ct = default);
    Task<DecryptedSshCredential> GetDecryptedAsync(CancellationToken ct = default);
    Task TouchAsync(CancellationToken ct = default);
}

public sealed class SshCredentialService(OrbitDbContext db, ITokenProtector protector, TimeProvider clock) : ISshCredentialService
{
    public async Task<SshCredentialStatusDto> GetStatusAsync(CancellationToken ct = default)
    {
        var cred = await Current(ct);
        if (cred is null) return SshCredentialStatusDto.NotConfigured;
        return new SshCredentialStatusDto
        {
            Configured = true,
            Username = cred.Username,
            Port = cred.Port,
            LastUsedAt = cred.LastUsedAt,
        };
    }

    public async Task SaveAsync(SaveSshKeyRequest request, CancellationToken ct = default)
    {
        var cred = await Current(ct);
        if (cred is null)
        {
            cred = new SshCredential { Id = Guid.NewGuid(), WorkspaceId = Seed.WorkspaceId, CreatedAt = clock.GetUtcNow() };
            db.SshCredentials.Add(cred);
        }

        cred.Username = string.IsNullOrWhiteSpace(request.Username) ? "root" : request.Username.Trim();
        cred.Port = request.Port is > 0 and <= 65535 ? request.Port : 22;
        cred.EncryptedPrivateKey = protector.Protect(request.PrivateKey.Trim() + "\n");
        cred.EncryptedPassphrase = string.IsNullOrEmpty(request.Passphrase) ? null : protector.Protect(request.Passphrase);

        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(CancellationToken ct = default)
        => await db.SshCredentials.Where(c => c.WorkspaceId == Seed.WorkspaceId).ExecuteDeleteAsync(ct);

    public async Task<DecryptedSshCredential> GetDecryptedAsync(CancellationToken ct = default)
    {
        var cred = await Current(ct) ?? throw new NoSshCredentialException();
        return new DecryptedSshCredential(
            protector.Unprotect(cred.EncryptedPrivateKey),
            cred.EncryptedPassphrase is null ? null : protector.Unprotect(cred.EncryptedPassphrase),
            cred.Username,
            cred.Port);
    }

    public async Task TouchAsync(CancellationToken ct = default)
    {
        var cred = await db.SshCredentials.FirstOrDefaultAsync(c => c.WorkspaceId == Seed.WorkspaceId, ct);
        if (cred is null) return;
        cred.LastUsedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
    }

    private Task<SshCredential?> Current(CancellationToken ct)
        => db.SshCredentials.FirstOrDefaultAsync(c => c.WorkspaceId == Seed.WorkspaceId, ct);
}
