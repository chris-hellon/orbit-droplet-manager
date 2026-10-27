using System.ComponentModel.DataAnnotations;

namespace Orbit.Shared.Contracts;

/// <summary>Payload for saving/replacing the provider API token.</summary>
public sealed class ConnectRequest
{
    [Required(ErrorMessage = "An API token is required.")]
    [MinLength(20, ErrorMessage = "That token looks too short to be a DigitalOcean personal access token.")]
    public string Token { get; set; } = "";

    public ProviderType Provider { get; set; } = ProviderType.DigitalOcean;

    /// <summary>Optional friendly label for the connection.</summary>
    public string? Label { get; set; }
}

/// <summary>Account facts returned by the provider when a token is validated.</summary>
public sealed record ProviderAccountDto
{
    public string Email { get; init; } = "";
    public string Uuid { get; init; } = "";
    public int DropletLimit { get; init; }
    public string Status { get; init; } = "";
}

/// <summary>Whether Orbit currently holds a working token, and non-secret details about it.</summary>
public sealed record ConnectionStatusDto
{
    public bool Connected { get; init; }
    public ProviderType Provider { get; init; } = ProviderType.DigitalOcean;
    public string? Label { get; init; }

    /// <summary>Last 4 characters of the stored token, e.g. "••••abcd". Never the full token.</summary>
    public string? TokenHint { get; init; }

    public string? AccountEmail { get; init; }
    public int DropletLimit { get; init; }
    public DateTimeOffset? LastValidatedAt { get; init; }

    public static ConnectionStatusDto Disconnected { get; } = new() { Connected = false };
}
