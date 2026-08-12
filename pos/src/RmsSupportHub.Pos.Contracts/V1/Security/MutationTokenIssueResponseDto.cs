namespace RmsSupportHub.Pos.Contracts.V1.Security;

/// <summary>
/// One short-lived, one-use mutation token. The opaque token is held in browser memory only.
/// </summary>
public sealed record MutationTokenIssueResponseDto(
    /// <summary>
    /// Opaque short-lived, one-use token bound to the authenticated Windows SID, exact Origin,
    /// operation, and server-resolved method. It is intended for browser memory only.
    /// </summary>
    string Token,
    /// <summary>UTC instant at which the Agent will reject the token as expired.</summary>
    DateTimeOffset ExpiresAtUtc);
