namespace RmsSupportHub.Pos.Domain.Models;

public enum MainServerEnvironment
{
    Testing,
    Production
}

public enum MainServerBindingState
{
    Bound,
    Unavailable,
    Mismatch,
    Ambiguous,
    Unknown
}

public enum MainServerReadOperation
{
    BranchStatus,
    InstalledBranch,
    InstalledPos
}

public enum MainServerReadOutcome
{
    NotAttempted,
    Succeeded,
    ActionRequired,
    OutcomeUnknown
}

/// <summary>
/// Internal server-owned Main Server profile. The base address and auth policy never cross the
/// Agent response boundary and cannot be selected or replaced by a browser request.
/// </summary>
public sealed record MainServerProfileDefinition(
    string ProfileId,
    MainServerEnvironment Environment,
    Uri BaseAddress,
    string ClientName,
    bool Enabled,
    IReadOnlyList<MainServerReadOperation> AllowedReadOperations);

public sealed record MainServerBinding(
    MainServerBindingState State,
    string? BranchCode,
    string? PosNumber,
    string? ClientName,
    string Detail);

public sealed record MainServerReadResult(
    MainServerReadOutcome Outcome,
    string? BranchState,
    string? PosState,
    string Detail);

public interface IMainServerProfileCatalog
{
    IReadOnlyList<MainServerProfileDefinition> GetProfiles();

    bool TryGetActive(RmsInstallationSnapshot installation, out MainServerProfileDefinition profile, out MainServerBinding binding);
}

public interface IMainServerReadOnlyClient
{
    Task<MainServerReadResult> ReadStateAsync(
        MainServerProfileDefinition profile,
        RmsInstallationSnapshot installation,
        CancellationToken cancellationToken = default);
}
