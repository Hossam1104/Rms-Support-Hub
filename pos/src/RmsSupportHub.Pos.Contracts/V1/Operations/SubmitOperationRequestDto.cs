namespace RmsSupportHub.Pos.Contracts.V1.Operations;

/// <summary>Requests a supported Agent operation. IDs and resource names are server policy, never paths or service names.</summary>
public sealed record SubmitOperationRequestDto(string OperationType, string BranchCodeSnapshot);
