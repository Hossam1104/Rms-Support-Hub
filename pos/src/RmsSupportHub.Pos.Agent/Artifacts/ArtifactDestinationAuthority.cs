using System.Security.Principal;

namespace RmsSupportHub.Pos.Agent.Artifacts;

/// <summary>
/// Dedicated export-only authority seam. The Agent retains source access, while all destination
/// creation, replacement, rename, and cleanup run under the authenticated pipe caller token.
/// </summary>
public interface IArtifactDestinationAuthority
{
    Task<T> RunAsync<T>(WindowsIdentity callerIdentity, Func<Task<T>> operation);

    Task RunAsync(WindowsIdentity callerIdentity, Func<Task> operation);
}

public sealed class WindowsArtifactDestinationAuthority : IArtifactDestinationAuthority
{
    public Task<T> RunAsync<T>(WindowsIdentity callerIdentity, Func<Task<T>> operation)
    {
        ArgumentNullException.ThrowIfNull(callerIdentity);
        ArgumentNullException.ThrowIfNull(operation);
        return WindowsIdentity.RunImpersonatedAsync(callerIdentity.AccessToken, operation);
    }

    public Task RunAsync(WindowsIdentity callerIdentity, Func<Task> operation)
    {
        ArgumentNullException.ThrowIfNull(callerIdentity);
        ArgumentNullException.ThrowIfNull(operation);
        return WindowsIdentity.RunImpersonatedAsync(callerIdentity.AccessToken, operation);
    }
}
