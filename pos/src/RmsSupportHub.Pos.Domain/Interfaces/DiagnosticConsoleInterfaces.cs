using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Domain.Interfaces;

/// <summary>Typed local console policy; callers cannot supply a process target.</summary>
public interface IDiagnosticConsolePolicy
{
    bool TryCreateLaunchSpec(
        DiagnosticConsoleTarget target,
        RmsInstallationSnapshot installation,
        out DiagnosticConsoleExecutableDefinition definition,
        out DiagnosticConsoleLaunchSpec? launchSpec,
        out string? failureCode);
}
