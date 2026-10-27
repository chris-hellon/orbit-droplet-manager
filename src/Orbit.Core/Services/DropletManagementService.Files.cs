using Orbit.Shared.Contracts;

namespace Orbit.Core.Services;

/// <summary>File-manager operations (SFTP) for a droplet: resolves the host server-side, then delegates
/// to <see cref="ISftpService"/> and logs mutating actions to the activity feed.</summary>
public sealed partial class DropletManagementService
{
    public async Task<DirectoryListingDto> ListFilesAsync(long dropletId, string? path, CancellationToken ct = default)
    {
        var host = await ResolveHostAsync(dropletId, ct);
        return await sftp.ListAsync(host, path, ct);
    }

    public async Task UploadFileAsync(long dropletId, string directory, string fileName, Stream content, CancellationToken ct = default)
    {
        var host = await ResolveHostAsync(dropletId, ct);
        await sftp.UploadAsync(host, directory, fileName, content, ct);
        await activity.RecordAsync("files", $"Uploaded {fileName} to {directory}", dropletId, "success", ct);
    }

    public async Task DownloadFileAsync(long dropletId, string path, Stream destination, CancellationToken ct = default)
    {
        var host = await ResolveHostAsync(dropletId, ct);
        await sftp.DownloadAsync(host, path, destination, ct);
    }

    public async Task<DirectoryListingDto> MakeDirectoryAsync(long dropletId, string directory, string name, CancellationToken ct = default)
    {
        var host = await ResolveHostAsync(dropletId, ct);
        await sftp.MakeDirectoryAsync(host, directory, name, ct);
        await activity.RecordAsync("files", $"Created folder {name} in {directory}", dropletId, "info", ct);
        return await sftp.ListAsync(host, directory, ct);
    }

    public async Task<DirectoryListingDto> DeleteFileAsync(long dropletId, string path, CancellationToken ct = default)
    {
        var host = await ResolveHostAsync(dropletId, ct);
        await sftp.DeleteAsync(host, path, ct);
        await activity.RecordAsync("files", $"Deleted {path}", dropletId, "warning", ct);
        return await sftp.ListAsync(host, ParentDir(path), ct);
    }

    public async Task<DirectoryListingDto> RenameFileAsync(long dropletId, string path, string newName, CancellationToken ct = default)
    {
        var host = await ResolveHostAsync(dropletId, ct);
        await sftp.RenameAsync(host, path, newName, ct);
        await activity.RecordAsync("files", $"Renamed {path} to {newName}", dropletId, "info", ct);
        return await sftp.ListAsync(host, ParentDir(path), ct);
    }

    private static string ParentDir(string path)
    {
        var p = path.TrimEnd('/');
        var idx = p.LastIndexOf('/');
        return idx <= 0 ? "/" : p[..idx];
    }
}
