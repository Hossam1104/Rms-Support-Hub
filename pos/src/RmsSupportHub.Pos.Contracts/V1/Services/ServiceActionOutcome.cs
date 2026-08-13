namespace RmsSupportHub.Pos.Contracts.V1.Services;

/// <summary>
/// Truth about one typed Windows-service control request. The outcome is deliberately separate
/// from the HTTP status: an accepted HTTP response can still carry a pre-dispatch rejection or an
/// ambiguous SCM result.
/// </summary>
public enum ServiceActionOutcome
{
    NotAttempted,
    Failed,
    Accepted,
    OutcomeUnknown
}
