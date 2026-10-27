using Microsoft.AspNetCore.SignalR;

namespace Orbit.Hubs;

/// <summary>
/// Streams live metrics and droplet-state changes to browsers. Clients only receive — all commands
/// go through the REST API — so the hub itself has no invokable server methods.
/// </summary>
public sealed class MetricsHub : Hub;
