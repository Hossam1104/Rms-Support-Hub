namespace RmsSupportHub.Pos.Domain.Interfaces;

/// <summary>
/// File-system capability used by the local backup workflow. The application layer owns the
/// backup policy and archive orchestration; this port owns Windows I/O, so the policy can be
/// exercised with fake storage without touching a real device.
/// </summary>
public interface IBackupFileSystem
{
    BackupDestinationInfo InspectDestination(string path);

    bool FileExists(string path);

    bool IsReparsePoint(string path);

    long GetFileLength(string path);

    Task EnsureDirectoryAsync(string path, CancellationToken cancellationToken = default);

    Task CopyFileAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken = default);

    Task<Stream> CreateFileAsync(string path, CancellationToken cancellationToken = default);

    Task MoveFileAsync(string sourcePath, string destinationPath, bool overwrite, CancellationToken cancellationToken = default);

    Task DeleteFileAsync(string path, CancellationToken cancellationToken = default);

    Task DeleteDirectoryAsync(string path, CancellationToken cancellationToken = default);

    Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken = default);
}

public sealed record BackupDestinationInfo(bool Exists, bool IsDirectory, long AvailableFreeSpaceBytes);
