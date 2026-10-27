using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using Orbit.Shared.Contracts;
using Renci.SshNet;
using Renci.SshNet.Common;
using Renci.SshNet.Sftp;

namespace Orbit.Core.Services;

/// <summary>
/// File transfer on a droplet over SFTP (which rides the same SSH connection / key as the shell —
/// no FTP daemon needed). Browse, upload, download, make folders, rename and delete.
/// Hosts are resolved server-side by the caller; paths are SFTP protocol arguments, not shell input.
/// </summary>
public interface ISftpService
{
    Task<DirectoryListingDto> ListAsync(string host, string? path, CancellationToken ct = default);
    Task UploadAsync(string host, string directory, string fileName, Stream content, CancellationToken ct = default);
    Task DownloadAsync(string host, string path, Stream destination, CancellationToken ct = default);
    Task MakeDirectoryAsync(string host, string directory, string name, CancellationToken ct = default);
    Task DeleteAsync(string host, string path, CancellationToken ct = default);
    Task RenameAsync(string host, string path, string newName, CancellationToken ct = default);
}

public sealed class SftpService(ISshCredentialService credentials, ILogger<SftpService> logger) : ISftpService
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(15);

    public async Task<DirectoryListingDto> ListAsync(string host, string? path, CancellationToken ct = default)
    {
        using var client = await ConnectAsync(host, ct);
        var dir = string.IsNullOrWhiteSpace(path) ? client.WorkingDirectory : RequireAbsolute(path!);

        var entries = await Task.Run(() => client.ListDirectory(dir)
            .Where(f => f.Name is not ("." or ".."))
            .Select(ToEntry)
            .OrderByDescending(e => e.IsDirectory)
            .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToList(), ct);

        await credentials.TouchAsync(ct);
        return new DirectoryListingDto { Path = dir, Parent = ParentOf(dir), Entries = entries };
    }

    public async Task UploadAsync(string host, string directory, string fileName, Stream content, CancellationToken ct = default)
    {
        var name = SafeName(fileName);
        var target = Combine(RequireAbsolute(directory), name);
        using var client = await ConnectAsync(host, ct);
        await using var remote = client.Create(target); // truncates/overwrites
        await content.CopyToAsync(remote, ct);
        await credentials.TouchAsync(ct);
    }

    public async Task DownloadAsync(string host, string path, Stream destination, CancellationToken ct = default)
    {
        using var client = await ConnectAsync(host, ct);
        await using var remote = client.OpenRead(RequireAbsolute(path));
        await remote.CopyToAsync(destination, ct);
        await credentials.TouchAsync(ct);
    }

    public async Task MakeDirectoryAsync(string host, string directory, string name, CancellationToken ct = default)
    {
        var target = Combine(RequireAbsolute(directory), SafeName(name));
        using var client = await ConnectAsync(host, ct);
        await Task.Run(() => client.CreateDirectory(target), ct);
        await credentials.TouchAsync(ct);
    }

    public async Task DeleteAsync(string host, string path, CancellationToken ct = default)
    {
        var target = RequireAbsolute(path);
        using var client = await ConnectAsync(host, ct);
        await Task.Run(() =>
        {
            var attrs = client.GetAttributes(target);
            if (attrs.IsDirectory) client.DeleteDirectory(target);
            else client.DeleteFile(target);
        }, ct);
        await credentials.TouchAsync(ct);
    }

    public async Task RenameAsync(string host, string path, string newName, CancellationToken ct = default)
    {
        var source = RequireAbsolute(path);
        var dir = ParentOf(source) ?? "/";
        var target = Combine(dir, SafeName(newName));
        using var client = await ConnectAsync(host, ct);
        await Task.Run(() => client.RenameFile(source, target), ct);
        await credentials.TouchAsync(ct);
    }

    // ---- helpers ----

    private static FileEntryDto ToEntry(ISftpFile f) => new()
    {
        Name = f.Name,
        IsDirectory = f.IsDirectory,
        IsSymlink = f.IsSymbolicLink,
        Size = f.Length,
        Modified = new DateTimeOffset(DateTime.SpecifyKind(f.LastWriteTimeUtc, DateTimeKind.Utc)),
        Permissions = Perms(f),
    };

    private static string Perms(ISftpFile f)
    {
        var type = f.IsDirectory ? 'd' : f.IsSymbolicLink ? 'l' : '-';
        return string.Create(10, f, (span, file) =>
        {
            span[0] = type;
            span[1] = file.OwnerCanRead ? 'r' : '-';
            span[2] = file.OwnerCanWrite ? 'w' : '-';
            span[3] = file.OwnerCanExecute ? 'x' : '-';
            span[4] = file.GroupCanRead ? 'r' : '-';
            span[5] = file.GroupCanWrite ? 'w' : '-';
            span[6] = file.GroupCanExecute ? 'x' : '-';
            span[7] = file.OthersCanRead ? 'r' : '-';
            span[8] = file.OthersCanWrite ? 'w' : '-';
            span[9] = file.OthersCanExecute ? 'x' : '-';
        });
    }

    private static string RequireAbsolute(string path)
    {
        var p = path.Trim();
        if (p.Length == 0 || p[0] != '/' || p.Contains('\0'))
            throw new SshAccessException("Invalid path.");
        // Collapse a trailing slash (except root) so parent/child maths stay simple.
        return p.Length > 1 ? p.TrimEnd('/') : p;
    }

    // A single path segment — no separators or traversal, so an upload/rename can't escape its directory.
    private static string SafeName(string name)
    {
        var n = name.Trim();
        if (n.Length == 0 || n is "." or ".." || n.Contains('/') || n.Contains('\\') || n.Contains('\0'))
            throw new SshAccessException($"\"{name}\" isn't a valid file name.");
        return n;
    }

    private static string Combine(string dir, string name) => dir == "/" ? $"/{name}" : $"{dir}/{name}";

    private static string? ParentOf(string path)
    {
        if (path == "/") return null;
        var idx = path.LastIndexOf('/');
        return idx <= 0 ? "/" : path[..idx];
    }

    private async Task<SftpClient> ConnectAsync(string host, CancellationToken ct)
    {
        var cred = await credentials.GetDecryptedAsync(ct);

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
        var client = new SftpClient(connInfo) { OperationTimeout = TimeSpan.FromMinutes(30) };

        try
        {
            await client.ConnectAsync(ct);
            return client;
        }
        catch (OperationCanceledException)
        {
            client.Dispose();
            throw;
        }
        catch (Exception ex)
        {
            client.Dispose();
            logger.LogDebug(ex, "SFTP connect to {Host}:{Port} failed.", host, cred.Port);
            throw new SshAccessException(ex switch
            {
                SshAuthenticationException =>
                    $"SSH authentication failed as '{cred.Username}'. Check the username and that this key's public half is in the droplet's ~/.ssh/authorized_keys.",
                SocketException =>
                    $"Couldn't reach {host}:{cred.Port}. Is the droplet running and is SSH open to this host?",
                SshOperationTimeoutException =>
                    $"Timed out connecting to {host}:{cred.Port}.",
                _ => $"SFTP connection failed: {ex.Message}",
            });
        }
    }
}
