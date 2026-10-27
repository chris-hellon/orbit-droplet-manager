using Orbit.Shared.Contracts;

namespace Orbit.Core.Services;

/// <summary>
/// Pushes live updates to connected browsers. Implemented in the web host over SignalR; abstracted
/// here so Core (and the background poller) never references ASP.NET SignalR types directly.
/// </summary>
public interface IMetricsNotifier
{
    Task MetricsAsync(DropletMetricsDto metrics, CancellationToken ct = default);
    Task DropletStateAsync(DropletDto droplet, CancellationToken ct = default);
    Task DropletsRefreshedAsync(CancellationToken ct = default);
}

/// <summary>No-op fallback so Core is usable without a hub (tests, design-time).</summary>
public sealed class NullMetricsNotifier : IMetricsNotifier
{
    public Task MetricsAsync(DropletMetricsDto metrics, CancellationToken ct = default) => Task.CompletedTask;
    public Task DropletStateAsync(DropletDto droplet, CancellationToken ct = default) => Task.CompletedTask;
    public Task DropletsRefreshedAsync(CancellationToken ct = default) => Task.CompletedTask;
}
