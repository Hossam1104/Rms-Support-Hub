namespace RmsSupportHub.Pos.Contracts.V1.Security;

/// <summary>
/// One short-lived, one-use mutation token. The opaque token is held in browser memory only.
/// </summary>
public sealed record MutationTokenIssueResponseDto(string Token, DateTimeOffset ExpiresAtUtc);
