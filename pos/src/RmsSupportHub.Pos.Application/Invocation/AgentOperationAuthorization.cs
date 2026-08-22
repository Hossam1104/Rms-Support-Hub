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

        if (risk is not AgentOperationRisk.ReadOnlyDiagnostic
            and not AgentOperationRisk.AdministratorOnlyMutation)
        {
            return AgentAuthorizationDecision.Deny(
                "operation_risk_unknown",
                "The requested operation risk is not supported.");
        }

        if (risk == AgentOperationRisk.AdministratorOnlyMutation)
        {
            return context.Source switch
            {
                InvocationSource.LegacyLoopbackHttp
                    when context.AuthorizationLevel == InvocationAuthorizationLevel.LocalAdministrator =>
                    AgentAuthorizationDecision.Allow(),
                InvocationSource.LocalWpf
                    when context.AuthorizationLevel == InvocationAuthorizationLevel.LocalAdministrator =>
                    AgentAuthorizationDecision.Allow(),
                _ => AgentAuthorizationDecision.Deny(
                    "administrator_authorization_required",
                    "Administrator authority is required for this operation.")
            };
        }

        return context.Source switch
        {
            InvocationSource.LegacyLoopbackHttp
                when context.AuthorizationLevel == InvocationAuthorizationLevel.LocalAdministrator =>
                AgentAuthorizationDecision.Allow(),
            InvocationSource.LocalWpf
                when context.AuthorizationLevel is
                    InvocationAuthorizationLevel.LocalOperator or
                    InvocationAuthorizationLevel.LocalAdministrator =>
                AgentAuthorizationDecision.Allow(),
            _ => AgentAuthorizationDecision.Deny(
                "diagnostic_authorization_required",
                "An authenticated authorized caller is required for this diagnostic operation.")
        };
    }
}
