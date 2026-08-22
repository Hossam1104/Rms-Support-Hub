using System.Security.AccessControl;
using System.Security.Principal;
using System.IO.Pipes;

namespace RmsSupportHub.Pos.LocalIpc;

public interface ILocalIpcOperatorGroupResolver
{
    bool TryResolve(string configuredGroupName, out SecurityIdentifier operatorGroupSid);
}

public interface ILocalIpcAccountSidResolver
{
    bool TryResolve(string accountName, out SecurityIdentifier sid);
}

public sealed class WindowsLocalIpcAccountSidResolver : ILocalIpcAccountSidResolver
{
    public bool TryResolve(string accountName, out SecurityIdentifier sid)
    {
        sid = null!;
        try
        {
            if (new NTAccount(accountName).Translate(typeof(SecurityIdentifier))
                is not SecurityIdentifier resolved)
            {
                return false;
            }

            sid = resolved;
            return true;
        }
        catch (IdentityNotMappedException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}

/// <summary>
/// Resolves only the explicitly configured local operator group. There is intentionally no broad
/// principal fallback when resolution fails.
/// </summary>
public sealed class WindowsLocalIpcOperatorGroupResolver : ILocalIpcOperatorGroupResolver
{
    private readonly ILocalIpcAccountSidResolver accountResolver;

    public WindowsLocalIpcOperatorGroupResolver(ILocalIpcAccountSidResolver? accountResolver = null)
    {
        this.accountResolver = accountResolver ?? new WindowsLocalIpcAccountSidResolver();
    }

    public bool TryResolve(string configuredGroupName, out SecurityIdentifier operatorGroupSid)
    {
        operatorGroupSid = null!;
        if (string.IsNullOrWhiteSpace(configuredGroupName)
            || configuredGroupName.Length > 256
            || configuredGroupName.Any(char.IsControl))
        {
            return false;
        }

        var separator = configuredGroupName.IndexOf('\\');
        if (separator >= 0)
        {
            if (separator == 0
                || separator != configuredGroupName.LastIndexOf('\\')
                || !string.Equals(
                    configuredGroupName[..separator],
                    Environment.MachineName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        var localAccountName = separator < 0
            ? $"{Environment.MachineName}\\{configuredGroupName}"
            : configuredGroupName;
        return accountResolver.TryResolve(localAccountName, out operatorGroupSid);
    }
}

/// <summary>
/// Builds the exact allow-list ACL for the local IPC endpoint. Protection is explicit and no
/// inherited or broad principals are retained.
/// </summary>
public static class LocalIpcSecurityDescriptor
{
    public static PipeSecurity Create(SecurityIdentifier operatorGroupSid)
    {
        ArgumentNullException.ThrowIfNull(operatorGroupSid);

        var security = new PipeSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        var localSystemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, domainSid: null);
        var administratorsSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, domainSid: null);
        AddAllow(security, localSystemSid, PipeAccessRights.FullControl);
        AddAllow(security, administratorsSid, PipeAccessRights.FullControl);
        AddAllow(
            security,
            operatorGroupSid,
            PipeAccessRights.ReadData
                | PipeAccessRights.WriteData
                | PipeAccessRights.ReadExtendedAttributes
                | PipeAccessRights.WriteExtendedAttributes
                | PipeAccessRights.ReadAttributes
                | PipeAccessRights.WriteAttributes
                | PipeAccessRights.ReadPermissions
                | PipeAccessRights.Synchronize);
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.NetworkSid, domainSid: null),
            PipeAccessRights.FullControl,
            AccessControlType.Deny));
        return security;
    }

    private static void AddAllow(PipeSecurity security, SecurityIdentifier sid, PipeAccessRights rights) =>
        security.AddAccessRule(new PipeAccessRule(
            sid,
            rights,
            AccessControlType.Allow));
}

public interface ILocalIpcSecurityDescriptorFactory
{
    PipeSecurity Create(SecurityIdentifier operatorGroupSid);
}

public sealed class WindowsLocalIpcSecurityDescriptorFactory : ILocalIpcSecurityDescriptorFactory
{
    public PipeSecurity Create(SecurityIdentifier operatorGroupSid) =>
        LocalIpcSecurityDescriptor.Create(operatorGroupSid);
}

public interface ILocalIpcServerIdentityVerifier
{
    bool IsExpectedServer(NamedPipeClientStream pipe);
}

/// <summary>
/// Verifies the connected pipe owner before the client sends a request. The expected SID is kept in
/// this verifier so a future service-account migration does not change the typed IPC client.
/// </summary>
public sealed class WindowsLocalIpcServerIdentityVerifier : ILocalIpcServerIdentityVerifier
{
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const uint TokenQuery = 0x0008;

    private readonly SecurityIdentifier expectedServerSid;

    public WindowsLocalIpcServerIdentityVerifier(SecurityIdentifier? expectedServerSid = null)
    {
        this.expectedServerSid = expectedServerSid
            ?? new SecurityIdentifier(WellKnownSidType.LocalSystemSid, domainSid: null);
    }

    public SecurityIdentifier ExpectedServerSid => expectedServerSid;

    public bool IsExpectedServer(NamedPipeClientStream pipe)
    {
        ArgumentNullException.ThrowIfNull(pipe);
        try
        {
            if (!pipe.IsConnected
                || !GetNamedPipeServerProcessId(pipe.SafePipeHandle.DangerousGetHandle(), out var processId)
                || processId == 0)
            {
                return false;
            }

            var processHandle = OpenProcess(ProcessQueryLimitedInformation, false, processId);
            if (processHandle == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                if (!OpenProcessToken(processHandle, TokenQuery, out var tokenHandle)
                    || tokenHandle.IsInvalid)
                {
                    return false;
                }

                using (tokenHandle)
                {
                    using var identity = new WindowsIdentity(tokenHandle.DangerousGetHandle());
                    tokenHandle.SetHandleAsInvalid();
                    return identity.User is { } actualSid && expectedServerSid.Equals(actualSid);
                }
            }
            finally
            {
                _ = CloseHandle(processHandle);
            }
        }
        catch (Exception)
        {
            return false;
        }
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetNamedPipeServerProcessId(IntPtr pipeHandle, out uint serverProcessId);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, uint processId);

    [System.Runtime.InteropServices.DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(
        IntPtr processHandle,
        uint desiredAccess,
        out Microsoft.Win32.SafeHandles.SafeAccessTokenHandle tokenHandle);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}
