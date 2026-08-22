namespace RmsSupportHub.Pos.Application.Invocation;

/// <summary>
/// Identifies the transport that entered the shared Agent application layer. The value is created
/// by a trusted transport adapter; it is never read from a command payload.
/// </summary>
public enum InvocationSource
{
    LegacyLoopbackHttp,
    LocalWpf,
    RemoteHub
}

/// <summary>Authorization facts derived by the Agent from the authenticated caller.</summary>
public enum InvocationAuthorizationLevel
{
    Unauthenticated,
    LocalOperator,
    LocalAdministrator,
    RemoteAdministrator
}

/// <summary>
/// Transport-agnostic caller facts passed to an application handler. Identity and authority are
/// intentionally explicit so a future transport cannot accidentally infer privilege from request
/// JSON or from a browser concept.
/// </summary>
public sealed record InvocationContext(
    InvocationSource Source,
    string AuthenticatedCaller,
    InvocationAuthorizationLevel AuthorizationLevel,
    string CorrelationId,
    string? DeviceIdentity = null,
    string? AdministratorIdentity = null);
