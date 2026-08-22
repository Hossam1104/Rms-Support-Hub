namespace RmsSupportHub.Pos.Application.Invocation;

public enum AgentOperationRisk
{
    ReadOnlyDiagnostic,
    AdministratorOnlyMutation
}

public sealed record AgentAuthorizationDecision(
    bool Allowed,
    string Code,
    string Message)
{
    public static AgentAuthorizationDecision Allow() =>
        new(true, "authorized", "The invocation is authorized.");

    public static AgentAuthorizationDecision Deny(string code, string message) =>
        new(false, code, message);
}

/// <summary>
/// Shared fail-closed authorization policy for typed Agent application operations.
/// </summary>
public static class AgentOperationAuthorization
{
    public static AgentAuthorizationDecision Authorize(
        InvocationContext? context,
        AgentOperationRisk risk)
    {
        if (context is null)
        {
            return AgentAuthorizationDecision.Deny(
                "invocation_context_missing",
                "An authenticated invocation context is required.");
        }

        if (string.IsNullOrWhiteSpace(context.AuthenticatedCaller)
            || string.IsNullOrWhiteSpace(context.CorrelationId)
            || context.CorrelationId.Length > 128
            || context.CorrelationId.Any(char.IsControl))
        {
            return AgentAuthorizationDecision.Deny(
                "invocation_context_invalid",
                "The authenticated invocation context is invalid.");
        }

        if (risk == AgentOperationRisk.AdministratorOnlyMutation)
        {
            return context.AuthorizationLevel is
                InvocationAuthorizationLevel.LocalAdministrator or
                InvocationAuthorizationLevel.RemoteAdministrator
                ? AgentAuthorizationDecision.Allow()
                : AgentAuthorizationDecision.Deny(
                    "administrator_authorization_required",
                    "Administrator authority is required for this operation.");
        }

        return context.AuthorizationLevel switch
        {
            InvocationAuthorizationLevel.LocalOperator
                or InvocationAuthorizationLevel.LocalAdministrator
                or InvocationAuthorizationLevel.RemoteAdministrator => AgentAuthorizationDecision.Allow(),
            _ => AgentAuthorizationDecision.Deny(
                "diagnostic_authorization_required",
                "An authenticated authorized caller is required for this diagnostic operation.")
        };
    }
}
