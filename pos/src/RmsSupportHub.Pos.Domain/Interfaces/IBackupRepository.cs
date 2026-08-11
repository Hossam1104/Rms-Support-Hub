using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Domain.Interfaces;

public interface IBackupRepository
{
    Task<IReadOnlyList<RemoteEntryInfo>> ListDirectoriesAsync(RemoteConnectionInfo connection, string rootFolder, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RemoteEntryInfo>> ListFilesAsync(RemoteConnectionInfo connection, string folder, CancellationToken cancellationToken = default);

    Task DownloadFileAsync(RemoteConnectionInfo connection, string remoteFilePath, string localFilePath, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
}
