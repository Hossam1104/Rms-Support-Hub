namespace RmsSupportHub.Pos.Domain.Interfaces;

/// <summary>
/// File-system seam used by the restore workflow. Restore policy never performs privileged file I/O
/// directly; the production implementation is supplied by the Windows infrastructure project and
/// tests use disposable temporary directories or deterministic fakes.
/// </summary>
public interface IRestoreFileSystem
{
    bool FileExists(string path);

    bool DirectoryExists(string path);

    bool IsReparsePoint(string path);

    IReadOnlyList<string> EnumerateFileSystemEntries(string directoryPath);

    long GetFileLength(string path);

    DateTimeOffset GetLastWriteTimeUtc(string path);

    long GetAvailableFreeSpace(string path);

    string GetFullPath(string path);

    Task EnsureDirectoryAsync(string path, CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken = default);

    Task<Stream> CreateFileAsync(string path, CancellationToken cancellationToken = default);

    Task CopyFileAtomicAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default);

    Task DeleteFileAsync(string path, CancellationToken cancellationToken = default);

    Task DeleteDirectoryAsync(string path, CancellationToken cancellationToken = default);

    Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken = default);
}
