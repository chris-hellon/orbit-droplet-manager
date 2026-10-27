using System.ComponentModel.DataAnnotations;

namespace Orbit.Shared.Contracts;

/// <summary>Whether Orbit has SSH access configured (non-secret details only).</summary>
public sealed record SshCredentialStatusDto
{
    public bool Configured { get; init; }
    public string Username { get; init; } = "root";
    public int Port { get; init; } = 22;
    public DateTimeOffset? LastUsedAt { get; init; }

    public static SshCredentialStatusDto NotConfigured { get; } = new() { Configured = false };
}

/// <summary>Save/replace the SSH private key Orbit uses to manage droplets.</summary>
public sealed class SaveSshKeyRequest
{
    [Required(ErrorMessage = "An SSH private key is required.")]
    public string PrivateKey { get; set; } = "";

    public string Username { get; set; } = "root";
    public int Port { get; set; } = 22;

    /// <summary>Passphrase for the key, if it is encrypted.</summary>
    public string? Passphrase { get; set; }
}

public sealed record SshTestResultDto
{
    public bool Ok { get; init; }
    public string Message { get; init; } = "";
}
