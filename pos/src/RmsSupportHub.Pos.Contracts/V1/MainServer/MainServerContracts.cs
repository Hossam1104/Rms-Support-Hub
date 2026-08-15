namespace RmsSupportHub.Pos.Contracts.V1.MainServer;

/// <summary>Server-owned environment profile. Production is future-facing and disabled until separately approved.</summary>
public enum MainServerEnvironmentDto
{
    Testing,
    Production
}

public enum MainServerBindingStateDto
{
    Bound,
    Unavailable,
    Mismatch,
    Ambiguous,
    Unknown
}

public enum MainServerReadOutcomeDto
{
    NotAttempted,
    Succeeded,
    ActionRequired,
    OutcomeUnknown
}

public enum MainServerReadOperationDto
{
    BranchStatus,
    InstalledBranch,
    InstalledPos
}

/// <summary>Safe profile projection. Credentials, authorization material, and the free base URL are never returned.</summary>
public sealed record MainServerProfileDto(
    string ProfileId,
    MainServerEnvironmentDto Environment,
    bool Enabled,
    MainServerBindingStateDto Binding,
    string ClientName,
    IReadOnlyList<MainServerReadOperationDto> AllowedReadOperations,
    string Detail);

public sealed record MainServerProfilesDto(
    IReadOnlyList<MainServerProfileDto> Profiles,
    string ActiveProfileId,
    MainServerBindingStateDto ActiveBinding,
    string Detail);

/// <summary>Safe Branch/POS installation-state evidence returned by a fixed read-only operation.</summary>
public sealed record MainServerStateEvidenceDto(
    string ProfileId,
    MainServerEnvironmentDto Environment,
    MainServerBindingStateDto Binding,
    MainServerReadOutcomeDto Outcome,
    string? BranchCode,
    string? PosNumber,
    string? ClientName,
    string? BranchState,
    string? PosState,
    string Detail,
    DateTimeOffset CheckedAtUtc,
    string CorrelationId);
