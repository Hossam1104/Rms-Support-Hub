namespace RmsSupportHub.Pos.Contracts.V1.Rms;

public sealed record RmsConsistencyDto(
    RmsConsistencyState BranchCode,
    RmsConsistencyState PosIdentity,
    RmsConsistencyState MainServerBranchId,
    RmsConsistencyState MainServerPosId,
    RmsConsistencyState Version,
    IReadOnlyList<string> Warnings);
