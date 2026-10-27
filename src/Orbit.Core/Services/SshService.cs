using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using Orbit.Shared.Contracts;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace Orbit.Core.Services;

/// <summary>Friendly, user-facing SSH failure (bad key, auth, unreachable host, ...).</summary>
public sealed class SshAccessException(string message) : Exception(message);

public sealed record SshResult(int ExitStatus, string Output, string Error)
{
    public bool Ok => ExitStatus == 0;
}

/// <summary>A live interactive shell over SSH. Owns the client; dispose to disconnect.</summary>
public sealed class ShellSession(SshClient client, ShellStream stream) : IDisposable
{
    public ShellStream Stream { get; } = stream;

    public void Dispose()
    {
        try { Stream.Dispose(); } catch { /* ignore */ }
        try { if (client.IsConnected) client.Disconnect(); } catch { /* ignore */ }
        client.Dispose();
    }
}

public interface ISshService
{
    Task<SshTestResultDto> TestAsync(string host, CancellationToken ct = default);
    Task<SshResult> RunAsync(string host, string command, CancellationToken ct = default);
    Task<ShellSession> OpenShellAsync(string host, int columns, int rows, CancellationToken ct = default);
}

public sealed class SshService(ISshCredentialService credentials, ILogger<SshService> logger) : ISshService
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(15);

    public async Task<SshTestResultDto> TestAsync(string host, CancellationToken ct = default)
    {
        try
        {
            var result = await RunAsync(host, "uname -sr && echo OK", ct);
            return new SshTestResultDto { Ok = result.Ok, Message = result.Ok ? result.Output.Trim() : result.Error.Trim() };
        }
        catch (SshAccessException ex)
        {
            return new SshTestResultDto { Ok = false, Message = ex.Message };
        }
    }

    public async Task<SshResult> RunAsync(string host, string command, CancellationToken ct = default)
    {
        var cred = await credentials.GetDecryptedAsync(ct);
        using var client = await ConnectAsync(host, cred, ct);
        try
        {
            var result = await Task.Run(() =>
            {
                using var cmd = client.CreateCommand(command);
                var output = cmd.Execute();
                return new SshResult(cmd.ExitStatus ?? -1, output ?? "", cmd.Error ?? "");
            }, ct);
            await credentials.TouchAsync(ct);
            return result;
        }
        finally
        {
            try { client.Disconnect(); } catch { /* ignore */ }
        }
    }

    public async Task<ShellSession> OpenShellAsync(string host, int columns, int rows, CancellationToken ct = default)
    {
        var cred = await credentials.GetDecryptedAsync(ct);
        var client = await ConnectAsync(host, cred, ct);
        try
        {
            var cols = (uint)Math.Clamp(columns, 20, 500);
            var rowCount = (uint)Math.Clamp(rows, 5, 200);
            var shell = client.CreateShellStream("xterm-256color", cols, rowCount, 800, 600, 8192);
            await credentials.TouchAsync(ct);
            return new ShellSession(client, shell);
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    private async Task<SshClient> ConnectAsync(string host, DecryptedSshCredential cred, CancellationToken ct)
    {
        PrivateKeyFile key;
        try
        {
            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(cred.PrivateKeyPem));
            key = string.IsNullOrEmpty(cred.Passphrase) ? new PrivateKeyFile(ms) : new PrivateKeyFile(ms, cred.Passphrase);
        }
        catch (Exception ex)
        {
            throw new SshAccessException($"Couldn't read the SSH private key — {ex.Message}. Make sure it's a full OpenSSH/PEM private key, including the BEGIN/END lines.");
        }

        var auth = new PrivateKeyAuthenticationMethod(cred.Username, key);
        var connInfo = new ConnectionInfo(host, cred.Port, cred.Username, auth) { Timeout = ConnectTimeout };
        var client = new SshClient(connInfo);

        try
        {
            await Task.Run(() => client.Connect(), ct);
            return client;
        }
        catch (Exception ex)
        {
            client.Dispose();
            logger.LogDebug(ex, "SSH connect to {Host}:{Port} failed.", host, cred.Port);
            throw new SshAccessException(FriendlyConnectError(ex, host, cred));
        }
    }

    private static string FriendlyConnectError(Exception ex, string host, DecryptedSshCredential cred) => ex switch
    {
        SshAuthenticationException =>
            $"SSH authentication failed as '{cred.Username}'. Check the username and that this key's public half is in the droplet's ~/.ssh/authorized_keys.",
        SocketException or System.Net.Sockets.SocketException =>
            $"Couldn't reach {host}:{cred.Port}. Is the droplet running and is SSH open to this host?",
        SshOperationTimeoutException =>
            $"Timed out connecting to {host}:{cred.Port}.",
        _ => $"SSH connection failed: {ex.Message}",
    };
}
