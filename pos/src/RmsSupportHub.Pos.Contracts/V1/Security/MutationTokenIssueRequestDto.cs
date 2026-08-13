namespace RmsSupportHub.Pos.Contracts.V1.Security;

/// <summary>
/// Requests one mutation token for a server-registered operation. The browser supplies only the
/// stable operation identifier; target paths and methods remain server-owned.
/// </summary>
public sealed record MutationTokenIssueRequestDto(
    /// <summary>
    /// Stable logical identifier for an operation registered by the Agent. The browser cannot
    /// supply the target path or HTTP method.
    /// </summary>
    string OperationId,
    /// <summary>
    /// Optional opaque server-issued target identifier. When the registered operation is target
    /// bound, the Agent resolves this identifier through its own allow-list and binds the token to
    /// the resulting canonical request path. A raw service name is never accepted.
    /// </summary>
    string? TargetId = null);
