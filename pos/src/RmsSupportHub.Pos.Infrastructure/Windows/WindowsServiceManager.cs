using System.ServiceProcess;
using System.Diagnostics;
using RmsSupportHub.Pos.Domain.Enums;
using RmsSupportHub.Pos.Domain.Interfaces;

namespace RmsSupportHub.Pos.Infrastructure.Windows;

public sealed class WindowsServiceManager : IServiceManager
{
    public Task<ServiceStatus> GetStatusAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!OperatingSystem.IsWindows())
            {
                return ServiceStatus.NotFound;
            }

            try
            {
                using var controller = new ServiceController(serviceName);
                return controller.Status switch
                {
                    ServiceControllerStatus.Running => ServiceStatus.Running,
                    ServiceControllerStatus.Stopped => ServiceStatus.Stopped,
                    _ => ServiceStatus.Unknown
                };
            }
            catch (InvalidOperationException)
            {
                return ServiceStatus.NotFound;
            }
            catch
            {
                return ServiceStatus.Unknown;
            }
        }, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, ServiceStatus>> GetStatusesAsync(IEnumerable<string> serviceNames, CancellationToken cancellationToken = default)
    {
        var statuses = new Dictionary<string, ServiceStatus>(StringComparer.OrdinalIgnoreCase);
        foreach (var serviceName in serviceNames)
        {
            statuses[serviceName] = await GetStatusAsync(serviceName, cancellationToken).ConfigureAwait(false);
        }

        return statuses;
    }

    public Task ControlAsync(string serviceName, ServiceControlAction action, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!OperatingSystem.IsWindows())
            {
                throw new PlatformNotSupportedException("Windows service control is only available on Windows.");
            }

            using var controller = new ServiceController(serviceName);
            switch (action)
            {
                case ServiceControlAction.Start:
                    Start(controller, cancellationToken);
                    break;
                case ServiceControlAction.Stop:
                    Stop(controller, cancellationToken);
                    break;
                case ServiceControlAction.Restart:
                    Stop(controller, cancellationToken);
                    Start(controller, cancellationToken);
                    break;
                case ServiceControlAction.Delete:
                    Delete(serviceName);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported service action.");
            }
        }, cancellationToken);
    }

    private static void Start(ServiceController controller, CancellationToken cancellationToken)
    {
        controller.Refresh();
        if (controller.Status == ServiceControllerStatus.Running)
        {
            return;
        }

        controller.Start();
        controller.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void Stop(ServiceController controller, CancellationToken cancellationToken)
    {
        controller.Refresh();
        if (controller.Status == ServiceControllerStatus.Stopped)
        {
            return;
        }

        if (!controller.CanStop)
        {
            throw new InvalidOperationException($"Service {controller.ServiceName} cannot be stopped.");
        }

        controller.Stop();
        controller.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void Delete(string serviceName)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "sc.exe",
            ArgumentList = { "delete", serviceName },
            UseShellExecute = false,
            CreateNoWindow = true
        });

        process?.WaitForExit(30_000);
        if (process is { ExitCode: not 0 })
        {
            throw new InvalidOperationException($"Failed to delete service {serviceName}.");
        }
    }
}
