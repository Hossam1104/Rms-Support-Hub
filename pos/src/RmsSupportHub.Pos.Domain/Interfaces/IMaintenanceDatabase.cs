using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Domain.Interfaces;

/// <summary>Server-side branch verification and bounded branch-reset scope preview seam.</summary>
public interface IMaintenanceDatabasePreview
{
    Task<bool> BranchExistsInDatabaseAsync(
        AppSettings settings,
        string databaseName,
        string branchCode,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MaintenanceTableScope>> GetBranchResetScopeAsync(
        AppSettings settings,
        string databaseName,
        string branchCode,
        IReadOnlyList<string> tableNames,
        CancellationToken cancellationToken = default);
}

/// <summary>Optional explicit database/table reset seam used by the Agent maintenance workflow.</summary>
public interface IMaintenanceDatabaseReset
{
    Task ResetBranchDataAsync(
        AppSettings settings,
        string databaseName,
        string branchCode,
        IReadOnlyList<string> tableNames,
        CancellationToken cancellationToken = default);
}
