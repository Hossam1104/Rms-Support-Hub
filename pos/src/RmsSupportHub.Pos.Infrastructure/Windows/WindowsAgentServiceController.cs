using System.ComponentModel;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using RmsSupportHub.Pos.Domain.Enums;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Infrastructure.Windows;

public interface IAgentServiceLifecycleController
{
    AgentServiceEvidence? ReadPermanent();

    bool EnsureConfigured(string executablePath);

    bool Stop(CancellationToken cancellationToken = default);

    bool Start(CancellationToken cancellationToken = default);

    bool Delete(CancellationToken cancellationToken = default);

    ServiceStatus GetStatus();
}

/// <summary>
/// Typed SCM adapter for the one permanent Agent service. It never accepts a caller-selected
/// service name and never exposes a generic SCM endpoint.
/// </summary>
public sealed class WindowsAgentServiceController : IAgentServiceLifecycleController
{
    private const uint ScManagerConnect = 0x0001;
    private const uint ScManagerCreateService = 0x0002;
    private const uint ServiceQueryConfig = 0x0001;
    private const uint ServiceChangeConfig = 0x0002;
    private const uint ServiceQueryStatus = 0x0004;
    private const uint ServiceStart = 0x0010;
    private const uint ServiceStop = 0x0020;
    private const uint DeleteAccess = 0x00010000;
    private const uint ServiceAllAccess = 0xF01FF;
    private const uint ServiceWin32OwnProcess = 0x00000010;
    private const uint ServiceAutoStart = 0x00000002;
    private const uint ServiceErrorNormal = 0x00000001;
    private const int ServiceConfigDescription = 1;
    private const int ServiceConfigFailureActions = 2;
    private const int ServiceActionRestart = 1;

    public AgentServiceEvidence? ReadPermanent()
    {
        if (!OperatingSystem.IsWindows()) return null;

        var manager = OpenSCManager(null, null, ScManagerConnect);
        if (manager == IntPtr.Zero) return null;
        try
        {
            var service = OpenService(manager, AgentProductIdentity.PermanentServiceName, ServiceQueryConfig | ServiceQueryStatus);
            if (service == IntPtr.Zero) return null;
            try
            {
                if (!TryQueryConfig(service, out var config)) return null;
                var displayName = TryGetDisplayName();
                var description = TryQueryDescription(service);
                var binaryPath = config.BinaryPathName;
                var hasArguments = !TryGetExactExecutable(binaryPath, out _);
                return new AgentServiceEvidence(
                    AgentProductIdentity.PermanentServiceName,
                    displayName,
                    description,
                    ExtractExecutable(binaryPath),
                    null,
                    null,
                    null,
                    null,
                    AgentResourceOwnershipState.Unknown)
                {
                    ServiceAccount = config.ServiceStartName,
                    StartType = config.StartType.ToString(),
                    HasArguments = hasArguments
                };
            }
            finally
            {
                CloseServiceHandle(service);
            }
        }
        finally
        {
            CloseServiceHandle(manager);
        }
    }

    public bool EnsureConfigured(string executablePath)
    {
        if (!OperatingSystem.IsWindows()
            || !TryGetExactExecutable('"' + executablePath + '"', out var exactPath)
            || !string.Equals(exactPath, Path.GetFullPath(executablePath), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var manager = OpenSCManager(null, null, ScManagerConnect | ScManagerCreateService);
        if (manager == IntPtr.Zero) return false;
        try
        {
            var service = OpenService(manager, AgentProductIdentity.PermanentServiceName, ServiceAllAccess);
            if (service == IntPtr.Zero)
            {
                service = CreateService(
                    manager,
                    AgentProductIdentity.PermanentServiceName,
                    AgentProductIdentity.ServiceDisplayName,
                    ServiceAllAccess,
                    ServiceWin32OwnProcess,
                    ServiceAutoStart,
                    ServiceErrorNormal,
                    '"' + Path.GetFullPath(executablePath) + '"',
                    null,
                    null,
                    null,
                    "LocalSystem",
                    null);
                if (service == IntPtr.Zero) return false;
            }

            try
            {
                if (!ChangeServiceConfig(
                    service,
                    ServiceWin32OwnProcess,
                    ServiceAutoStart,
                    ServiceErrorNormal,
                    '"' + Path.GetFullPath(executablePath) + '"',
                    null,
                    IntPtr.Zero,
                    null,
                    "LocalSystem",
                    null,
                    AgentProductIdentity.ServiceDisplayName))
                {
                    return false;
                }

                var description = new SERVICE_DESCRIPTION
                {
                    Description = Marshal.StringToHGlobalUni(AgentProductIdentity.ServiceDescription)
                };
                try
                {
                    if (!ChangeServiceConfig2(service, ServiceConfigDescription, ref description)) return false;
                }
                finally
                {
                    Marshal.FreeHGlobal(description.Description);
                }

                var recoveryActions = new[]
                {
                    new SC_ACTION { Type = ServiceActionRestart, Delay = 60_000 },
                    new SC_ACTION { Type = ServiceActionRestart, Delay = 60_000 },
                    new SC_ACTION { Type = ServiceActionRestart, Delay = 60_000 }
                };
                var actionBuffer = Marshal.AllocHGlobal(Marshal.SizeOf<SC_ACTION>() * recoveryActions.Length);
                var failureActions = IntPtr.Zero;
                try
                {
                    for (var index = 0; index < recoveryActions.Length; index++)
                    {
                        Marshal.StructureToPtr(
                            recoveryActions[index],
                            IntPtr.Add(actionBuffer, index * Marshal.SizeOf<SC_ACTION>()),
                            fDeleteOld: false);
                    }

                    failureActions = Marshal.AllocHGlobal(Marshal.SizeOf<SERVICE_FAILURE_ACTIONS>());
                    Marshal.StructureToPtr(
                        new SERVICE_FAILURE_ACTIONS
                        {
                            ResetPeriod = 86_400,
                            RebootMessage = IntPtr.Zero,
                            Command = IntPtr.Zero,
                            ActionsCount = recoveryActions.Length,
                            Actions = actionBuffer
                        },
                        failureActions,
                        fDeleteOld: false);
                    if (!ChangeServiceConfig2(service, ServiceConfigFailureActions, failureActions)) return false;
                }
                finally
                {
                    if (failureActions != IntPtr.Zero) Marshal.FreeHGlobal(failureActions);
                    Marshal.FreeHGlobal(actionBuffer);
                }

                return true;
            }
            finally
            {
                CloseServiceHandle(service);
            }
        }
        finally
        {
            CloseServiceHandle(manager);
        }
    }

    public bool Stop(CancellationToken cancellationToken = default) =>
        Control(ServiceControlAction.Stop, cancellationToken);

    public bool Start(CancellationToken cancellationToken = default) =>
        Control(ServiceControlAction.Start, cancellationToken);

    public bool Delete(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows()) return false;
        var manager = OpenSCManager(null, null, ScManagerConnect);
        if (manager == IntPtr.Zero) return false;
        try
        {
            var service = OpenService(manager, AgentProductIdentity.PermanentServiceName, DeleteAccess | ServiceQueryStatus | ServiceStop);
            if (service == IntPtr.Zero) return true;
            try
            {
                var status = GetStatus();
                if (status is ServiceStatus.Unknown) return false;
                if (status is ServiceStatus.Running or ServiceStatus.Transitioning && !Stop(cancellationToken)) return false;
                return DeleteService(service);
            }
            finally
            {
                CloseServiceHandle(service);
            }
        }
        finally
        {
            CloseServiceHandle(manager);
        }
    }

    public ServiceStatus GetStatus()
    {
        if (!OperatingSystem.IsWindows()) return ServiceStatus.NotFound;
        var manager = OpenSCManager(null, null, ScManagerConnect);
        if (manager == IntPtr.Zero) return ServiceStatus.Unknown;
        try
        {
            var service = OpenService(manager, AgentProductIdentity.PermanentServiceName, ServiceQueryStatus);
            if (service == IntPtr.Zero) return ServiceStatus.NotFound;
            try { return ServiceStatusFor(service); }
            finally { CloseServiceHandle(service); }
        }
        finally { CloseServiceHandle(manager); }
    }

    private bool Control(ServiceControlAction action, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows()) return false;
        var manager = OpenSCManager(null, null, ScManagerConnect);
        if (manager == IntPtr.Zero) return false;
        try
        {
            var access = ServiceQueryStatus | (action == ServiceControlAction.Start ? ServiceStart : ServiceStop);
            var service = OpenService(manager, AgentProductIdentity.PermanentServiceName, access);
            if (service == IntPtr.Zero) return false;
            try
            {
                var desired = action == ServiceControlAction.Start ? ServiceControllerStatus.Running : ServiceControllerStatus.Stopped;
                var current = TryGetNativeStatus(service);
                if (current is null) return false;
                if (current == desired) return true;
                if (action == ServiceControlAction.Start)
                {
                    if (!StartService(service, 0, IntPtr.Zero))
                    {
                        var error = Marshal.GetLastWin32Error();
                        if (error != 1056) return false; // ERROR_SERVICE_ALREADY_RUNNING
                    }
                }
                else
                {
                    var status = new SERVICE_STATUS();
                    if (!ControlService(service, 0x00000001, ref status))
                    {
                        var error = Marshal.GetLastWin32Error();
                        if (error != 1062) return false; // ERROR_SERVICE_NOT_ACTIVE
                    }
                }

                var deadline = DateTime.UtcNow.AddSeconds(30);
                while (DateTime.UtcNow < deadline)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var status = TryGetNativeStatus(service);
                    if (status is null) return false;
                    if (status == desired) return true;
                    Thread.Sleep(100);
                }

                return false;
            }
            finally
            {
                CloseServiceHandle(service);
            }
        }
        finally
        {
            CloseServiceHandle(manager);
        }
    }

    private static bool TryQueryConfig(IntPtr service, out ServiceConfig config)
    {
        config = default;
        var bufferSize = 16 * 1024;
        var buffer = Marshal.AllocHGlobal(bufferSize);
        try
        {
            if (!QueryServiceConfig(service, buffer, bufferSize, out _)) return false;
            var native = Marshal.PtrToStructure<QUERY_SERVICE_CONFIG>(buffer);
            config = new(
                Marshal.PtrToStringUni(native.BinaryPathName) ?? string.Empty,
                Marshal.PtrToStringUni(native.ServiceStartName) ?? string.Empty,
                native.StartType);
            return true;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static string? TryQueryDescription(IntPtr service)
    {
        try
        {
            var buffer = Marshal.AllocHGlobal(8 * 1024);
            try
            {
                if (!QueryServiceConfig2(service, ServiceConfigDescription, buffer, 8 * 1024, out _)) return null;
                var description = Marshal.PtrToStructure<SERVICE_DESCRIPTION>(buffer);
                return description.Description == IntPtr.Zero ? null : Marshal.PtrToStringUni(description.Description);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch
        {
            return null;
        }
    }

    private static string? TryGetDisplayName()
    {
        try
        {
            using var controller = new ServiceController(AgentProductIdentity.PermanentServiceName);
            return controller.DisplayName;
        }
        catch
        {
            return null;
        }
    }

    private static ServiceStatus ServiceStatusFor(IntPtr service) => TryGetNativeStatus(service) switch
    {
        ServiceControllerStatus.Running => ServiceStatus.Running,
        ServiceControllerStatus.Stopped => ServiceStatus.Stopped,
        ServiceControllerStatus.StartPending or ServiceControllerStatus.StopPending => ServiceStatus.Transitioning,
        null => ServiceStatus.Unknown,
        _ => ServiceStatus.Unknown
    };

    private static ServiceControllerStatus? TryGetNativeStatus(IntPtr service)
    {
        var status = new SERVICE_STATUS_PROCESS();
        if (!QueryServiceStatusEx(service, 0, ref status, Marshal.SizeOf<SERVICE_STATUS_PROCESS>(), out _)) return null;
        return status.CurrentState switch
        {
            2 => ServiceControllerStatus.StartPending,
            3 => ServiceControllerStatus.StopPending,
            4 => ServiceControllerStatus.Running,
            5 => ServiceControllerStatus.ContinuePending,
            6 => ServiceControllerStatus.PausePending,
            7 => ServiceControllerStatus.Paused,
            _ => ServiceControllerStatus.Stopped
        };
    }

    private static bool TryGetExactExecutable(string? binaryPath, out string executablePath)
    {
        executablePath = string.Empty;
        if (string.IsNullOrWhiteSpace(binaryPath)) return false;
        var value = binaryPath.Trim();
        if (!value.StartsWith('"'))
        {
            if (value.Any(char.IsWhiteSpace)) return false;
            executablePath = value;
            return true;
        }

        var end = value.IndexOf('"', 1);
        if (end <= 1 || !string.IsNullOrWhiteSpace(value[(end + 1)..])) return false;
        executablePath = value[1..end];
        return true;
    }

    private static string? ExtractExecutable(string? binaryPath) => TryGetExactExecutable(binaryPath, out var path) ? path : null;

    private readonly record struct ServiceConfig(string BinaryPathName, string ServiceStartName, uint StartType);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct QUERY_SERVICE_CONFIG
    {
        public uint ServiceType;
        public uint StartType;
        public uint ErrorControl;
        public IntPtr BinaryPathName;
        public IntPtr LoadOrderGroup;
        public IntPtr TagId;
        public IntPtr Dependencies;
        public IntPtr ServiceStartName;
        public IntPtr DisplayName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SERVICE_DESCRIPTION
    {
        public IntPtr Description;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SC_ACTION
    {
        public int Type;
        public int Delay;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SERVICE_FAILURE_ACTIONS
    {
        public int ResetPeriod;
        public IntPtr RebootMessage;
        public IntPtr Command;
        public int ActionsCount;
        public IntPtr Actions;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SERVICE_STATUS
    {
        public uint ServiceType;
        public uint CurrentState;
        public uint ControlsAccepted;
        public uint Win32ExitCode;
        public uint ServiceSpecificExitCode;
        public uint CheckPoint;
        public uint WaitHint;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SERVICE_STATUS_PROCESS
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

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr OpenSCManager(string? machineName, string? databaseName, uint desiredAccess);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr OpenService(IntPtr manager, string serviceName, uint desiredAccess);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateService(
        IntPtr manager,
        string serviceName,
        string displayName,
        uint desiredAccess,
        uint serviceType,
        uint startType,
        uint errorControl,
        string binaryPathName,
        string? loadOrderGroup,
        string? tagId,
        string? dependencies,
        string? serviceStartName,
        string? password);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool ChangeServiceConfig(
        IntPtr service,
        uint serviceType,
        uint startType,
        uint errorControl,
        string? binaryPathName,
        string? loadOrderGroup,
        IntPtr tagId,
        string? dependencies,
        string? serviceStartName,
        string? password,
        string? displayName);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool ChangeServiceConfig2(IntPtr service, int infoLevel, ref SERVICE_DESCRIPTION info);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool ChangeServiceConfig2(IntPtr service, int infoLevel, IntPtr info);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool QueryServiceConfig(IntPtr service, IntPtr buffer, int bufferSize, out int bytesNeeded);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool QueryServiceConfig2(IntPtr service, int infoLevel, IntPtr buffer, int bufferSize, out int bytesNeeded);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool QueryServiceStatusEx(IntPtr service, int infoLevel, ref SERVICE_STATUS_PROCESS status, int bufferSize, out int bytesNeeded);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool StartService(IntPtr service, int argumentCount, IntPtr arguments);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool ControlService(IntPtr service, uint control, ref SERVICE_STATUS status);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool DeleteService(IntPtr service);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool CloseServiceHandle(IntPtr handle);
}
