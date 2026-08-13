namespace RmsSupportHub.Pos.Agent.Services;

public static class ServiceActionCodes
{
    public const string InvalidRequest = "service_action_request_invalid";
    public const string InvalidIdempotencyKey = "idempotency_key_invalid";
    public const string TargetNotAllowListed = "service_target_not_allowlisted";
    public const string StateUnavailable = "service_state_unavailable";
    public const string ActionNotAllowed = "service_action_not_allowed";
    public const string IdempotencyConflict = "idempotency_key_conflict";
    public const string DuplicateInProgress = "service_action_in_progress";
    public const string ServiceBusy = "service_busy";
    public const string IdempotencyCapacity = "idempotency_capacity";
    public const string Accepted = "service_action_accepted";
    public const string Failed = "service_action_failed";
    public const string OutcomeUnknown = "service_action_outcome_unknown";
}
