using RmsSupportHub.Pos.Application.Maintenance;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Application.Services;

/// <summary>
/// Compatibility facade for the retained WinUI workflow. The Agent uses <see
/// cref="MaintenanceService"/> directly so browser requests cannot bypass challenge and operation
/// controls. Both paths share the same canonical policy and fakeable privileged seams.
/// </summary>
public sealed class CleanupService(
    IDatabaseService databaseService,
    IServiceManager serviceManager,
    IMaintenanceFileSystem fileSystem)
{
    private readonly MaintenanceService _maintenance = new(databaseService, serviceManager, fileSystem);

    public async Task<OperationResult> CleanupFilesAsync(
        AppSettings settings,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _maintenance.ExecuteCleanupAsync(settings, progress: progress, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return result.Operation;
    }

    public async Task<OperationResult> ResetBranchDataAsync(
        AppSettings settings,
        CancellationToken cancellationToken = default)
    {
        var result = await _maintenance.ExecuteBranchResetAsync(settings, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return result.Operation;
    }
}
