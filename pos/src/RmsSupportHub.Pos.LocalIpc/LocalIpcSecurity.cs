using System.Security.AccessControl;
using System.Security.Principal;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using RmsSupportHub.Pos.Contracts;

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
/// Resolves the process that owns a connected local Named Pipe. This is deliberately separate from
/// service identity resolution so both native lookups can be deterministic in tests.
/// </summary>
public interface ILocalIpcPipeServerProcessIdResolver
{
    bool TryGetServerProcessId(NamedPipeClientStream pipe, out uint processId);
}

public sealed class WindowsLocalIpcPipeServerProcessIdResolver : ILocalIpcPipeServerProcessIdResolver
{
    public bool TryGetServerProcessId(NamedPipeClientStream pipe, out uint processId)
    {
        ArgumentNullException.ThrowIfNull(pipe);
        processId = 0;
        try
        {
            if (!pipe.IsConnected || pipe.SafePipeHandle.IsInvalid)
            {
                return false;
            }

            return GetNamedPipeServerProcessId(pipe.SafePipeHandle, out processId)
                && processId > 0;
        }
        catch (Exception)
        {
            processId = 0;
            return false;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetNamedPipeServerProcessId(
        SafePipeHandle pipeHandle,
        out uint serverProcessId);
}

/// <summary>
/// Resolves a canonical Windows Service's currently running process using read-only SCM access.
/// A false result covers missing services, access failures, transitional states, and unavailable
/// process IDs so callers can fail closed.
/// </summary>
public interface ILocalServiceProcessResolver
{
    bool TryGetRunningServiceProcessId(string serviceName, out uint processId);
}

public sealed class WindowsLocalServiceProcessResolver : ILocalServiceProcessResolver
{
    // SC_MANAGER_CONNECT and SERVICE_QUERY_STATUS are the only rights needed by this resolver.
    public const uint RequestedServiceManagerAccess = 0x0001;
    public const uint RequestedServiceAccess = 0x0004;

    private const int ServiceStatusProcessInfo = 0;
    private const uint ServiceRunning = 0x00000004;

    public bool TryGetRunningServiceProcessId(string serviceName, out uint processId)
    {
        ArgumentNullException.ThrowIfNull(serviceName);
        processId = 0;
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            return false;
        }

        try
        {
            using var manager = OpenScManager(
                null,
                null,
                RequestedServiceManagerAccess);
            if (manager.IsInvalid)
            {
                return false;
            }

            using var service = OpenService(manager, serviceName, RequestedServiceAccess);
            if (service.IsInvalid)
            {
                return false;
            }

            if (!QueryServiceStatusEx(
                    service,
                    ServiceStatusProcessInfo,
                    out var status,
                    (uint)Marshal.SizeOf<ServiceStatusProcess>(),
                    out _)
                || status.CurrentState != ServiceRunning
                || status.ProcessId == 0)
            {
                return false;
            }

            processId = status.ProcessId;
            return true;
        }
        catch (Exception)
        {
            processId = 0;
            return false;
        }
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeScmHandle OpenScManager(
        string? machineName,
        string? databaseName,
        uint desiredAccess);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeServiceHandle OpenService(
        SafeScmHandle manager,
        string serviceName,
        uint desiredAccess);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool QueryServiceStatusEx(
        SafeServiceHandle service,
        int infoLevel,
        out ServiceStatusProcess status,
        uint bufferSize,
        out uint bytesNeeded);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool CloseServiceHandle(IntPtr handle);

    [StructLayout(LayoutKind.Sequential)]
    private struct ServiceStatusProcess
    {
        public uint ServiceType;
        public uint CurrentState;
        public uint ControlsAccepted;
        public uint Win32ExitCode;
        public uint ServiceSpecificExitCode;
        public uint CheckPoint;
        public uint WaitHint;
        public uint ProcessId;
        public uint ServiceFlags;
    }

    private sealed class SafeScmHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeScmHandle() : base(ownsHandle: true) { }

        protected override bool ReleaseHandle() => CloseServiceHandle(handle);
    }

    private sealed class SafeServiceHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeServiceHandle() : base(ownsHandle: true) { }

        protected override bool ReleaseHandle() => CloseServiceHandle(handle);
    }
}

/// <summary>
/// Verifies the connected pipe before the client sends a request by matching its server PID to the
/// currently running canonical RmsSupportAgent Windows Service PID. It does not inspect process
/// tokens and does not require SeDebugPrivilege.
/// </summary>
public sealed class WindowsLocalIpcServerIdentityVerifier : ILocalIpcServerIdentityVerifier
{
    private readonly ILocalIpcPipeServerProcessIdResolver pipeProcessIdResolver;
    private readonly ILocalServiceProcessResolver serviceProcessResolver;

    public WindowsLocalIpcServerIdentityVerifier(
        ILocalIpcPipeServerProcessIdResolver? pipeProcessIdResolver = null,
        ILocalServiceProcessResolver? serviceProcessResolver = null)
    {
        this.pipeProcessIdResolver = pipeProcessIdResolver
            ?? new WindowsLocalIpcPipeServerProcessIdResolver();
        this.serviceProcessResolver = serviceProcessResolver
            ?? new WindowsLocalServiceProcessResolver();
    }

    public string ExpectedServiceName => AgentServiceIdentity.PermanentServiceName;

    public bool IsExpectedServer(NamedPipeClientStream pipe)
    {
        ArgumentNullException.ThrowIfNull(pipe);
        try
        {
            if (!pipe.IsConnected
                || !pipeProcessIdResolver.TryGetServerProcessId(pipe, out var pipeServerProcessId)
                || pipeServerProcessId == 0
                || !serviceProcessResolver.TryGetRunningServiceProcessId(
                    ExpectedServiceName,
                    out var serviceProcessId)
                || serviceProcessId == 0)
            {
                return false;
            }

            return pipeServerProcessId == serviceProcessId;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
