using Orbit.Shared;
using Orbit.Shared.Contracts;

namespace Orbit.Core.Providers;

/// <summary>
/// A cloud provider Orbit can manage. DigitalOcean is implemented; Hetzner/Linode can be added by
/// writing another implementation and registering it — nothing else in the app knows the difference.
/// Implementations are stateless: the caller passes the (already-decrypted) token on every call.
/// </summary>
public interface IDropletProvider
{
    ProviderType Provider { get; }

    /// <summary>Verify a token and return the account it belongs to. Throws <see cref="ProviderException"/> on failure.</summary>
    Task<ProviderAccountDto> ValidateTokenAsync(string token, CancellationToken ct = default);

    Task<IReadOnlyList<DropletDto>> ListDropletsAsync(string token, CancellationToken ct = default);

    Task<IReadOnlyList<RegionDto>> ListRegionsAsync(string token, CancellationToken ct = default);

    /// <summary>Current CPU/RAM/disk for one droplet via the provider's monitoring API.</summary>
    Task<DropletMetricsDto> GetMetricsAsync(string token, long dropletId, CancellationToken ct = default);

    Task<DropletActionDto> PowerActionAsync(string token, long dropletId, PowerAction action, CancellationToken ct = default);

    Task<DropletActionDto> SnapshotAsync(string token, long dropletId, string name, CancellationToken ct = default);

    Task<IReadOnlyList<SizeDto>> ListSizesAsync(string token, CancellationToken ct = default);

    Task<IReadOnlyList<ImageDto>> ListImagesAsync(string token, CancellationToken ct = default);

    Task<DropletActionDto> ResizeAsync(string token, long dropletId, string size, bool resizeDisk, CancellationToken ct = default);

    Task<DropletActionDto> RebuildAsync(string token, long dropletId, string image, CancellationToken ct = default);
}

/// <summary>Resolves the right <see cref="IDropletProvider"/> for a connection's provider type.</summary>
public interface IProviderResolver
{
    IDropletProvider Get(ProviderType provider);
}

public sealed class ProviderResolver(IEnumerable<IDropletProvider> providers) : IProviderResolver
{
    private readonly IReadOnlyDictionary<ProviderType, IDropletProvider> _byType =
        providers.ToDictionary(p => p.Provider);

    public IDropletProvider Get(ProviderType provider) =>
        _byType.TryGetValue(provider, out var p)
            ? p
            : throw new ProviderException($"Provider '{provider}' is not supported yet.");
}

/// <summary>Raised when a provider API call fails in a way worth surfacing to the user.</summary>
public sealed class ProviderException(string message, int? statusCode = null) : Exception(message)
{
    public int? StatusCode { get; } = statusCode;
}
