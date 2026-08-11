namespace RmsSupportHub.Pos.Contracts.V1.Files;

/// <summary>
/// An opaque, short-lived, single-purpose handle bound to the issuing principal and re-validated at
/// use time (plan section 5.7) — never a capability to read arbitrary bytes later.
/// </summary>
public sealed record FileHandleDto(string HandleId, FileHandlePurpose Purpose, DateTimeOffset ExpiresAtUtc);
