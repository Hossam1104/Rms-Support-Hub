namespace RmsSupportHub.Pos.Contracts.V1.Operations;

public sealed record CancelOperationResponseDto(string OperationId, OperationState State);
