namespace RmsSupportHub.Pos.Contracts.V1.Operations;

/// <summary>
/// The operation state machine from plan section 5.3. Deliberately has no <c>Interrupted</c> state
/// and no resumability classification — agent restart clears the in-memory registry, matching the
/// application being replaced (plan section 0.3, section 4.2).
/// </summary>
public enum OperationState
{
    Queued,
    Running,
    Succeeded,
    PartiallySucceeded,
    Failed,
    Cancelled,
}
