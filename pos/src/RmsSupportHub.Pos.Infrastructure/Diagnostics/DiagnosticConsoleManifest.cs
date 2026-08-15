using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Infrastructure.Diagnostics;

public sealed class DiagnosticConsoleManifest : IDiagnosticConsoleManifest
{
    private readonly IReadOnlyDictionary<DiagnosticConsoleTarget, DiagnosticConsoleExecutableDefinition> definitions =
        new Dictionary<DiagnosticConsoleTarget, DiagnosticConsoleExecutableDefinition>
        {
            [DiagnosticConsoleTarget.BranchServerApi] = new(
                DiagnosticConsoleTarget.BranchServerApi,
                "Branch Server API diagnostics",
                "RMS.POS.BranchServerApi.exe",
                "branch",
                ["--diagnostic"],
                TimeSpan.FromSeconds(30),
                64 * 1024,
                512),
            [DiagnosticConsoleTarget.CashierServerApi] = new(
                DiagnosticConsoleTarget.CashierServerApi,
                "Cashier Server API diagnostics",
                "RMS.POS.CashierServerApi.exe",
                "cashier",
                ["--diagnostic"],
                TimeSpan.FromSeconds(30),
                64 * 1024,
                512),
            [DiagnosticConsoleTarget.ServiceManager] = new(
                DiagnosticConsoleTarget.ServiceManager,
                "RMS Services Manager diagnostics",
                "Rms.Pos.ServiceManager.exe",
                "services-manager",
                ["--diagnostic"],
                TimeSpan.FromSeconds(30),
                64 * 1024,
                512),
            [DiagnosticConsoleTarget.CashierUi] = new(
                DiagnosticConsoleTarget.CashierUi,
                "Cashier UI diagnostics",
                "RMS.Pos.Cashier.UI.exe",
                "cashier-ui",
                ["--diagnostic"],
                TimeSpan.FromSeconds(30),
                64 * 1024,
                512)
        };

    public bool TryGet(DiagnosticConsoleTarget target, out DiagnosticConsoleExecutableDefinition definition) =>
        definitions.TryGetValue(target, out definition!);
}
