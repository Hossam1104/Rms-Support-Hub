namespace RmsSupportHub.Pos.Domain.Interfaces;

public enum PrivilegedMutationLeaseState
{
    Acquired,
    Busy,
    Unavailable
}

public sealed record PrivilegedMutationLeaseAttempt(
    PrivilegedMutationLeaseState State,
    IDisposable? Handle,
    string Detail);

/// <summary>
/// One machine-wide lease for every privileged package/repair lifecycle mutation. The handle must
/// remain held until the caller has terminal or recovery truth, then be disposed in a finally block.
/// </summary>
public interface IPrivilegedMutationLease
{
    PrivilegedMutationLeaseAttempt TryAcquire(string operationScope, string principalSid);
}
