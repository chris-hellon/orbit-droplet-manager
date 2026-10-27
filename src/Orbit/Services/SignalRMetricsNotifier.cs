using Microsoft.AspNetCore.SignalR;
using Orbit.Core.Services;
using Orbit.Hubs;
using Orbit.Shared;
using Orbit.Shared.Contracts;

namespace Orbit.Services;

/// <summary>SignalR-backed implementation of <see cref="IMetricsNotifier"/> used by the host.</summary>
public sealed class SignalRMetricsNotifier(IHubContext<MetricsHub> hub) : IMetricsNotifier
{
    public Task MetricsAsync(DropletMetricsDto metrics, CancellationToken ct = default)
        => hub.Clients.All.SendAsync(MetricsHubMethods.ReceiveMetrics, metrics, ct);

    public Task DropletStateAsync(DropletDto droplet, CancellationToken ct = default)
        => hub.Clients.All.SendAsync(MetricsHubMethods.ReceiveDropletState, droplet, ct);

    public Task DropletsRefreshedAsync(CancellationToken ct = default)
        => hub.Clients.All.SendAsync(MetricsHubMethods.ReceiveDropletsRefreshed, ct);
}
