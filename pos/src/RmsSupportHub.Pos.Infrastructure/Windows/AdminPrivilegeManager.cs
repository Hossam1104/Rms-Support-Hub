using System.Diagnostics;
using System.Security.Principal;

namespace RmsSupportHub.Pos.Infrastructure.Windows;

public sealed class AdminPrivilegeManager
{
    private const string SkipElevationVariable = "POS_ADMIN_SKIP_ELEVATION";

    public bool IsAdministrator()
    {
        if (!OperatingSystem.IsWindows())
        {
            return true;
        }

        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public bool RequestAdministrator()
    {
        if (!OperatingSystem.IsWindows() || IsAdministrator() || ShouldSkipElevation())
        {
            return false;
        }

        var executable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executable))
        {
            return false;
        }

        var args = string.Join(' ', Environment.GetCommandLineArgs().Skip(1).Select(QuoteArgument));
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = executable,
                Arguments = args,
                Verb = "runas",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Normal
            });

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string QuoteArgument(string value)
    {
        return string.Concat('"', value.Replace("\"", "\\\"", StringComparison.Ordinal), '"');
    }

    private static bool ShouldSkipElevation()
    {
        var value = Environment.GetEnvironmentVariable(SkipElevationVariable);
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }
}
