using System.Threading;
using RmsSupportHub.Pos.Domain.Interfaces;

namespace RmsSupportHub.Pos.Infrastructure.Configuration;

/// <summary>
/// A non-blocking named semaphore is the cross-process lifecycle boundary. A same-process fallback
/// keeps tests deterministic on non-Windows hosts; no caller can bypass a held lease by changing
/// principal, idempotency key, operation kind, or endpoint.
/// </summary>
public sealed class MachineWidePrivilegedMutationLease : IPrivilegedMutationLease, IDisposable
{
    private const string SemaphoreName = "Global\\RmsSupportHub.Pos.Agent.PrivilegedMutationLease";
    private static readonly SemaphoreSlim NonWindowsSemaphore = new(1, 1);
    private readonly Semaphore? machineSemaphore;

    public MachineWidePrivilegedMutationLease()
    {
        if (!OperatingSystem.IsWindows()) return;

        try
        {
            machineSemaphore = new Semaphore(1, 1, SemaphoreName);
        }
        catch
        {
            machineSemaphore = null;
        }
    }

    public PrivilegedMutationLeaseAttempt TryAcquire(string operationScope, string principalSid)
    {
        if (string.IsNullOrWhiteSpace(operationScope) || string.IsNullOrWhiteSpace(principalSid))
        {
            return new(PrivilegedMutationLeaseState.Unavailable, null, "The privileged lifecycle lease identity is invalid.");
        }

        if (OperatingSystem.IsWindows())
        {
            if (machineSemaphore is null)
            {
                return new(PrivilegedMutationLeaseState.Unavailable, null, "The machine-wide privileged lifecycle lease could not be opened.");
            }

            try
            {
                return machineSemaphore.WaitOne(0)
                    ? new(PrivilegedMutationLeaseState.Acquired, new ReleaseHandle(machineSemaphore), "The machine-wide privileged lifecycle lease was acquired.")
                    : new(PrivilegedMutationLeaseState.Busy, null, "Another privileged package or repair lifecycle is active on this machine.");
            }
            catch
            {
                return new(PrivilegedMutationLeaseState.Unavailable, null, "The machine-wide privileged lifecycle lease could not be acquired safely.");
            }
        }

        return NonWindowsSemaphore.Wait(0)
            ? new(PrivilegedMutationLeaseState.Acquired, new ReleaseHandle(NonWindowsSemaphore), "The process-wide privileged lifecycle lease was acquired.")
            : new(PrivilegedMutationLeaseState.Busy, null, "Another privileged package or repair lifecycle is active in this process.");
    }

    public void Dispose() => machineSemaphore?.Dispose();

    private sealed class ReleaseHandle(IDisposable semaphore) : IDisposable
    {
        private int released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref released, 1) != 0) return;
            switch (semaphore)
            {
                case Semaphore named:
                    named.Release();
                    break;
                case SemaphoreSlim local:
                    local.Release();
                    break;
            }
        }
    }
}
