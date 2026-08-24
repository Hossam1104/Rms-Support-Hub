using RmsSupportHub.Pos.Contracts.V1.Rms;
using RmsSupportHub.Pos.Contracts.V1.LocalIpc;
using RmsSupportHub.Pos.Desktop.Wpf.Models;

namespace RmsSupportHub.Pos.Desktop.Wpf.Services;

public interface ILocalBackupClient
{
    Task<BackupInventoryResult> GetInventoryAsync(CancellationToken cancellationToken = default);

    Task<BackupOperationResult> CreateAsync(
        RmsDatabaseTarget target,
        CancellationToken cancellationToken = default);

    Task<ArtifactExportResult> ExportAsync(
        LocalArtifactExportRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record LocalArtifactExportRequest(
    LocalIpcArtifactKind ArtifactKind,
    string ArtifactId,
    RmsDatabaseTarget? DatabaseTarget,
    string DestinationPath,
    bool OverwriteConfirmed);
