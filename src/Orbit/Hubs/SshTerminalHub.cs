using System.Collections.Concurrent;
using System.Text;
using Microsoft.AspNetCore.SignalR;
using Orbit.Core.Services;
using Renci.SshNet.Common;

namespace Orbit.Hubs;

/// <summary>
/// Holds one live SSH shell per SignalR connection and pumps its output back to that browser.
/// Singleton: the hub is transient per call, so session state can't live on it.
/// </summary>
public sealed class SshTerminalSessions(
    IServiceScopeFactory scopeFactory,
    IHubContext<SshTerminalHub> hub,
    ILogger<SshTerminalSessions> logger)
{
    private sealed record Entry(ShellSession Session, EventHandler<ShellDataEventArgs> Handler);

    private readonly ConcurrentDictionary<string, Entry> _sessions = new();

    public async Task StartAsync(string connectionId, long dropletId, int cols, int rows)
    {
        Stop(connectionId); // replace any existing shell on this connection

        await using var scope = scopeFactory.CreateAsyncScope();
        var mgmt = scope.ServiceProvider.GetRequiredService<IDropletManagementService>();
        var ssh = scope.ServiceProvider.GetRequiredService<ISshService>();

        var host = await mgmt.ResolveHostAsync(dropletId);
        var session = await ssh.OpenShellAsync(host, cols, rows);

        void OnData(object? _, ShellDataEventArgs e)
        {
            var text = Encoding.UTF8.GetString(e.Data);
            _ = hub.Clients.Client(connectionId).SendAsync("Output", text);
        }

        session.Stream.DataReceived += OnData;
        _sessions[connectionId] = new Entry(session, OnData);

        await hub.Clients.Client(connectionId).SendAsync("Ready");
    }

    public void Write(string connectionId, string data)
    {
        if (!_sessions.TryGetValue(connectionId, out var entry)) return;
        try
        {
            var bytes = Encoding.UTF8.GetBytes(data);
            entry.Session.Stream.Write(bytes, 0, bytes.Length);
            entry.Session.Stream.Flush();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "SSH shell write failed for {Conn}.", connectionId);
            Stop(connectionId);
        }
    }

    public void Stop(string connectionId)
    {
        if (_sessions.TryRemove(connectionId, out var entry))
        {
            try { entry.Session.Stream.DataReceived -= entry.Handler; } catch { /* ignore */ }
            entry.Session.Dispose();
        }
    }
}

/// <summary>Terminal hub: the browser calls Start/Send; the server streams "Output" back.</summary>
public sealed class SshTerminalHub(SshTerminalSessions sessions, ILogger<SshTerminalHub> logger) : Hub
{
    public async Task Start(long dropletId, int cols, int rows)
    {
        try
        {
            await sessions.StartAsync(Context.ConnectionId, dropletId, cols, rows);
        }
        catch (NoSshCredentialException ex)
        {
            await Clients.Caller.SendAsync("Error", ex.Message);
        }
        catch (SshAccessException ex)
        {
            await Clients.Caller.SendAsync("Error", ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Terminal start failed.");
            await Clients.Caller.SendAsync("Error", "Failed to open the SSH session.");
        }
    }

    public void Send(string data) => sessions.Write(Context.ConnectionId, data);

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        sessions.Stop(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
