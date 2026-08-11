using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Domain.Interfaces;

public interface IDatabaseService : IAsyncDisposable
{
    Task TestConnectionAsync(AppSettings settings, CancellationToken cancellationToken = default);

    Task<bool> BranchExistsAsync(AppSettings settings, string branchCode, CancellationToken cancellationToken = default);

    Task ResetBranchDataAsync(AppSettings settings, string branchCode, CancellationToken cancellationToken = default);

    Task BackupDatabaseAsync(AppSettings settings, string databaseName, string backupFilePath, bool useCompatibilityMode, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RestoreFileInfo>> ReadRestoreFileListAsync(AppSettings settings, string backupFilePath, CancellationToken cancellationToken = default);

    Task RestoreDatabaseAsync(AppSettings settings, string targetDatabase, string backupFilePath, IReadOnlyList<RestoreFileInfo> logicalFiles, string dbFilesPath, CancellationToken cancellationToken = default);
}
