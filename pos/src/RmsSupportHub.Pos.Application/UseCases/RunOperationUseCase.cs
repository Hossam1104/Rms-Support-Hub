using RmsSupportHub.Pos.Application.Services;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Application.UseCases;

public sealed class RunOperationUseCase(
    BackupService backupService,
    RestoreService restoreService,
    BranchVerificationService branchVerificationService,
    CleanupService cleanupService)
{
    public Task<OperationResult> VerifyBranchAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        return branchVerificationService.VerifyAsync(settings, cancellationToken);
    }

    public Task<OperationResult> BackupAsync(AppSettings settings, IReadOnlyCollection<string> selectedItems, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        return backupService.BackupAsync(settings, selectedItems, progress, cancellationToken);
    }

    public Task<OperationResult> RestoreAsync(AppSettings settings, string backupZip, string? targetDatabase = null, string? dbFilesPath = null, string restoreType = "Full", IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        return restoreService.RestoreAsync(settings, backupZip, targetDatabase, dbFilesPath, restoreType, progress, cancellationToken);
    }

    public Task<OperationResult> CleanupAsync(AppSettings settings, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        return cleanupService.CleanupFilesAsync(settings, progress, cancellationToken);
    }

    public Task<OperationResult> ResetBranchDataAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        return cleanupService.ResetBranchDataAsync(settings, cancellationToken);
    }
}
