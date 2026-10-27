using Orbit.Shared;

namespace Orbit.Core.Data;

/// <summary>
/// A tenant boundary. There is one seeded workspace today, but the model is multi-tenant from
/// day one so additional users/workspaces can be added without a schema rewrite.
/// </summary>
public class Workspace
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }

    public List<User> Users { get; set; } = [];
    public List<ProviderConnection> Connections { get; set; } = [];
}

/// <summary>A member of a workspace. Single-user for now; the owner is seeded.</summary>
public class User
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Workspace? Workspace { get; set; }

    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool IsOwner { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// A stored provider credential. The token itself is encrypted at rest with ASP.NET Core Data
/// Protection — only <see cref="EncryptedToken"/> is persisted, never the plaintext.
/// </summary>
public class ProviderConnection
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Workspace? Workspace { get; set; }

    public ProviderType Provider { get; set; } = ProviderType.DigitalOcean;
    public string? Label { get; set; }

    /// <summary>Data-Protection protected API token. Decrypt via <c>ITokenProtector</c> right before use.</summary>
    public string EncryptedToken { get; set; } = "";

    /// <summary>Last 4 chars of the plaintext token, for display only.</summary>
    public string TokenLast4 { get; set; } = "";

    public string? AccountEmail { get; set; }
    public int DropletLimit { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastValidatedAt { get; set; }
}

/// <summary>
/// SSH access used to manage the inside of droplets (services, sites, logs, shell). The private key
/// is encrypted at rest with Data Protection, same as the API token. One credential per workspace,
/// reused across droplets (the common "same key on every box" setup).
/// </summary>
public class SshCredential
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Workspace? Workspace { get; set; }

    public string Username { get; set; } = "root";
    public int Port { get; set; } = 22;

    /// <summary>Data-Protection protected OpenSSH private key (PEM).</summary>
    public string EncryptedPrivateKey { get; set; } = "";

    /// <summary>Data-Protection protected passphrase for the key, if it has one.</summary>
    public string? EncryptedPassphrase { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
}

/// <summary>A record of an action Orbit performed, shown in the dashboard activity feed.</summary>
public class ActivityEntry
{
    public long Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public DateTimeOffset TimestampUtc { get; set; }

    /// <summary>power | snapshot | resize | rebuild | service | site | firewall | database | connection.</summary>
    public string Category { get; set; } = "";
    public long? DropletId { get; set; }
    public string Message { get; set; } = "";

    /// <summary>info | success | warning.</summary>
    public string Kind { get; set; } = "info";
}

/// <summary>Well-known seeded identifiers for the single-user bootstrap.</summary>
public static class Seed
{
    public static readonly Guid WorkspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid OwnerUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    /// <summary>Fixed timestamp so EF migrations stay deterministic (never DateTimeOffset.Now in seed data).</summary>
    public static readonly DateTimeOffset Epoch = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
}
