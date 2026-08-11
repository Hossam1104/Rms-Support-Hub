namespace RmsSupportHub.Pos.Domain.Models;

/// <summary>
/// Service-owned maintenance policy.  These values are intentionally not part of the browser
/// configuration contract: paths are policy inputs for the Agent, not client-selected targets.
/// Empty or invalid managed, data, protected, or install safety-root lists fail closed for cleanup.
/// </summary>
public sealed class MaintenanceSettings
{
    public List<string> CleanupTargets { get; set; } = [];

    public List<string> ManagedRoots { get; set; } = [];

    public List<string> DataRoots { get; set; } = [];

    public List<string> InstallRoots { get; set; } = [];

    public List<string> ProtectedRoots { get; set; } = [];

    public bool AllowUncPaths { get; set; }

    public bool RejectReparsePoints { get; set; } = true;

    public bool StopOnServiceFailure { get; set; } = true;

    public bool ContinueAfterTargetFailure { get; set; } = true;

    public string BranchResetDatabase { get; set; } = string.Empty;

    public List<string> BranchResetTables { get; set; } =
    [
        "Sales",
        "CashierSessions",
        "InventoryMovements"
    ];

    public MaintenanceSettings Clone() => new()
    {
        CleanupTargets = CleanupTargets is null ? [] : [.. CleanupTargets],
        ManagedRoots = ManagedRoots is null ? [] : [.. ManagedRoots],
        DataRoots = DataRoots is null ? [] : [.. DataRoots],
        InstallRoots = InstallRoots is null ? [] : [.. InstallRoots],
        ProtectedRoots = ProtectedRoots is null ? [] : [.. ProtectedRoots],
        AllowUncPaths = AllowUncPaths,
        RejectReparsePoints = RejectReparsePoints,
        StopOnServiceFailure = StopOnServiceFailure,
        ContinueAfterTargetFailure = ContinueAfterTargetFailure,
        BranchResetDatabase = BranchResetDatabase,
        BranchResetTables = BranchResetTables is null ? [] : [.. BranchResetTables]
    };
}
