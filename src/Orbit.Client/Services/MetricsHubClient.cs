using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Orbit.Shared;
using Orbit.Shared.Contracts;

namespace Orbit.Client.Services;

/// <summary>
/// Wraps the SignalR connection to the metrics hub and surfaces strongly-typed events the pages
/// subscribe to. One instance per circuit/component; dispose to disconnect.
/// </summary>
public sealed class MetricsHubClient(NavigationManager nav) : IAsyncDisposable
{
    private HubConnection? _connection;

    public event Action<DropletMetricsDto>? MetricsReceived;
    public event Action<DropletDto>? DropletStateChanged;
    public event Action? DropletsRefreshed;
    public event Action<HubConnectionState>? ConnectionStateChanged;

    public HubConnectionState State => _connection?.State ?? HubConnectionState.Disconnected;

    public async Task StartAsync(CancellationToken ct = default)
    {
        if (_connection is not null) return;

        _connection = new HubConnectionBuilder()
            .WithUrl(nav.ToAbsoluteUri(ApiRoutes.Hubs.Metrics))
            .WithAutomaticReconnect()
            .Build();

        _connection.On<DropletMetricsDto>(MetricsHubMethods.ReceiveMetrics, m => MetricsReceived?.Invoke(m));
        _connection.On<DropletDto>(MetricsHubMethods.ReceiveDropletState, d => DropletStateChanged?.Invoke(d));
        _connection.On(MetricsHubMethods.ReceiveDropletsRefreshed, () => DropletsRefreshed?.Invoke());

        _connection.Reconnected += _ => { ConnectionStateChanged?.Invoke(HubConnectionState.Connected); return Task.CompletedTask; };
        _connection.Reconnecting += _ => { ConnectionStateChanged?.Invoke(HubConnectionState.Reconnecting); return Task.CompletedTask; };
        _connection.Closed += _ => { ConnectionStateChanged?.Invoke(HubConnectionState.Disconnected); return Task.CompletedTask; };

        await _connection.StartAsync(ct);
        ConnectionStateChanged?.Invoke(_connection.State);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }
}
