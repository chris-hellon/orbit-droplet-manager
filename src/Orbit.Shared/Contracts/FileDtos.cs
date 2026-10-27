using System.ComponentModel.DataAnnotations;

namespace Orbit.Shared.Contracts;

/// <summary>A single entry in a droplet directory listing (over SFTP).</summary>
public sealed record FileEntryDto
{
    public string Name { get; init; } = "";
    public bool IsDirectory { get; init; }
    public bool IsSymlink { get; init; }
    public long Size { get; init; }
    public DateTimeOffset Modified { get; init; }
    /// <summary>Unix-style permission string, e.g. "drwxr-xr-x".</summary>
    public string Permissions { get; init; } = "";
}

/// <summary>A directory listing on a droplet plus where we are in the tree.</summary>
public sealed record DirectoryListingDto
{
    /// <summary>The absolute path that was listed (resolved server-side; starts from the SSH user's home when none is given).</summary>
    public string Path { get; init; } = "/";
    /// <summary>Parent directory, or null at the filesystem root.</summary>
    public string? Parent { get; init; }
    public IReadOnlyList<FileEntryDto> Entries { get; init; } = [];
}

public sealed class MakeDirRequest
{
    /// <summary>Directory the new folder is created in.</summary>
    [Required] public string Path { get; set; } = "";
    [Required, MinLength(1)] public string Name { get; set; } = "";
}

public sealed class RenameFileRequest
{
    /// <summary>Full path of the item being renamed.</summary>
    [Required] public string Path { get; set; } = "";
    [Required, MinLength(1)] public string NewName { get; set; } = "";
}

public sealed class DeleteFileRequest
{
    /// <summary>Full path of the file or (empty) directory to delete.</summary>
    [Required] public string Path { get; set; } = "";
}
