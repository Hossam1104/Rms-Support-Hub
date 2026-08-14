namespace RmsSupportHub.Pos.Contracts.V1.Rms;

public enum RmsDatabaseTarget
{
    Branch,
    Cashier
}

public enum RmsDatabaseOperationKind
{
    Backup,
    Restore
}

public enum RmsDatabaseOperationState
{
    NotAttempted,
    Accepted,
    Running,
    Completed,
    Failed,
    OutcomeUnknown
}

/// <summary>Typed truth for an RMS database mutation or its long-running state.</summary>
public enum RmsDatabaseOperationOutcome
{
    NotAttempted,
    Accepted,
    Completed,
    Failed,
    OutcomeUnknown
}
