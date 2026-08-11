namespace RmsSupportHub.Pos.Contracts.V1.Security;

/// <summary>
/// Requests one mutation token for a server-registered operation. The browser supplies only the
/// stable operation identifier; target paths and methods remain server-owned.
/// </summary>
public sealed record MutationTokenIssueRequestDto(string OperationId);
