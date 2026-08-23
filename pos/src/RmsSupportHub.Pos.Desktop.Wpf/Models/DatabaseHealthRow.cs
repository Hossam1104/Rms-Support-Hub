using RmsSupportHub.Pos.Contracts.V1.Rms;

namespace RmsSupportHub.Pos.Desktop.Wpf.Models;

public sealed record DatabaseHealthRow(
    RmsDatabaseTarget DatabaseKind,
    string DisplayName,
    string ExpectedDatabase,
    string? ConfiguredDatabase,
    string? ServerDisplay,
    bool Configured,
    bool? DatabaseNameMatches,
    RmsDatabaseDiagnosticStatus Status,
    string SafeDetail,
    DateTimeOffset CheckedAtUtc)
{
    public string KindLabel => DatabaseKind switch
    {
        RmsDatabaseTarget.Branch => "Branch",
        RmsDatabaseTarget.Cashier => "Cashier",
        _ => "Unknown"
    };

    public string StatusLabel => Status switch
    {
        RmsDatabaseDiagnosticStatus.Reachable => "Connected",
        RmsDatabaseDiagnosticStatus.NotConfigured => "Not configured",
        RmsDatabaseDiagnosticStatus.ConfigurationInvalid => "Configuration invalid",
        RmsDatabaseDiagnosticStatus.DatabaseNameMismatch => "Database mismatch",
        RmsDatabaseDiagnosticStatus.AuthenticationFailed => "Authentication failed",
        RmsDatabaseDiagnosticStatus.DatabaseUnavailable => "Database unavailable",
        RmsDatabaseDiagnosticStatus.Unreachable => "Server unreachable",
        _ => "Unknown"
    };

    public string StatusGlyph => Status switch
    {
        RmsDatabaseDiagnosticStatus.Reachable => "●",
        RmsDatabaseDiagnosticStatus.NotConfigured => "—",
        RmsDatabaseDiagnosticStatus.ConfigurationInvalid => "?",
        _ => "▲"
    };

    public string ConfiguredLabel => Configured ? "Yes" : "No";

    public string NameMatchLabel => DatabaseNameMatches switch
    {
        true => "Yes",
        false => "No",
        _ => "Unknown"
    };

    public string ConfiguredDatabaseDisplay => ConfiguredDatabase ?? "Not reported";

    public string ServerDisplayValue => ServerDisplay ?? "Not reported";

    public bool IsConnected => Status == RmsDatabaseDiagnosticStatus.Reachable;

    public static bool TryCreate(
        RmsDatabaseHealthItemDto item,
        out DatabaseHealthRow? row)
    {
        row = null;
        if (!Enum.IsDefined(item.DatabaseKind)
            || !Enum.IsDefined(item.Status)
            || !IsSafeText(item.DisplayName, 128)
            || !IsSafeText(item.ExpectedDatabase, 128)
            || !IsSafeText(item.ConfiguredDatabase, 128)
            || !IsSafeText(item.ServerDisplay, 256)
            || !IsSafeText(item.SafeDetail, 128)
            || item.CheckedAtUtc == default)
        {
            return false;
        }

        var (expected, displayName) = item.DatabaseKind switch
        {
            RmsDatabaseTarget.Branch => ("RmsBranchSrv", "Branch Database"),
            RmsDatabaseTarget.Cashier => ("RmsCashierSrv", "Cashier Database"),
            _ => (string.Empty, string.Empty)
        };
        if (!string.Equals(item.ExpectedDatabase, expected, StringComparison.Ordinal)
            || !string.Equals(item.DisplayName, displayName, StringComparison.Ordinal))
        {
            return false;
        }

        var requiresConfiguredData = item.Status is
            RmsDatabaseDiagnosticStatus.DatabaseNameMismatch
                or RmsDatabaseDiagnosticStatus.Reachable
                or RmsDatabaseDiagnosticStatus.AuthenticationFailed
                or RmsDatabaseDiagnosticStatus.DatabaseUnavailable
                or RmsDatabaseDiagnosticStatus.Unreachable;
        if (requiresConfiguredData
            && (!item.Configured
                || string.IsNullOrWhiteSpace(item.ConfiguredDatabase)
                || string.IsNullOrWhiteSpace(item.ServerDisplay)))
        {
            return false;
        }

        if (item.Status == RmsDatabaseDiagnosticStatus.NotConfigured && item.Configured)
        {
            return false;
        }

        if (item.Status == RmsDatabaseDiagnosticStatus.Reachable
            && item.DatabaseNameMatches != true)
        {
            return false;
        }

        if (item.Status == RmsDatabaseDiagnosticStatus.DatabaseNameMismatch
            && item.DatabaseNameMatches != false)
        {
            return false;
        }

        if (item.DatabaseNameMatches == true
            && string.IsNullOrWhiteSpace(item.ConfiguredDatabase))
        {
            return false;
        }

        if (item.DatabaseNameMatches == false
            && item.Status != RmsDatabaseDiagnosticStatus.DatabaseNameMismatch)
        {
            return false;
        }

        row = new(
            item.DatabaseKind,
            displayName,
            expected,
            item.ConfiguredDatabase,
            item.ServerDisplay,
            item.Configured,
            item.DatabaseNameMatches,
            item.Status,
            SafeDetailFor(item.Status),
            item.CheckedAtUtc);
        return true;
    }

    private static string SafeDetailFor(RmsDatabaseDiagnosticStatus status) => status switch
    {
        RmsDatabaseDiagnosticStatus.Reachable => "Connected",
        RmsDatabaseDiagnosticStatus.NotConfigured => "Not configured",
        RmsDatabaseDiagnosticStatus.ConfigurationInvalid => "Configuration invalid",
        RmsDatabaseDiagnosticStatus.DatabaseNameMismatch => "Database mismatch",
        RmsDatabaseDiagnosticStatus.AuthenticationFailed => "Authentication failed",
        RmsDatabaseDiagnosticStatus.DatabaseUnavailable => "Database unavailable",
        RmsDatabaseDiagnosticStatus.Unreachable => "Server unreachable",
        _ => "Database health is unknown."
    };

    private static bool IsSafeText(string? value, int maximumLength)
    {
        if (value is null)
        {
            return true;
        }

        if (value.Length > maximumLength || value.Any(char.IsControl))
        {
            return false;
        }

        var lower = value.ToLowerInvariant();
        return !lower.Contains("password", StringComparison.Ordinal)
            && !lower.Contains("connectionstring", StringComparison.Ordinal)
            && !lower.Contains("user id", StringComparison.Ordinal)
            && !lower.Contains("uid=", StringComparison.Ordinal)
            && !lower.Contains("pwd=", StringComparison.Ordinal)
            && !lower.Contains("exception", StringComparison.Ordinal)
            && !lower.Contains("stack trace", StringComparison.Ordinal)
            && !lower.Contains("config path", StringComparison.Ordinal)
            && !lower.Contains("select ", StringComparison.Ordinal);
    }
}
