using RmsSupportHub.Pos.Application.Invocation;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Application.Services;

/// <summary>
/// Shared, transport-independent read-only projection for the two canonical RMS databases.
/// Periodic health polling is intentionally non-audited, just like service health polling.
/// </summary>
public sealed class DatabaseHealthQueryHandler
{
    public const string Operation = "rms.databases.health";
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    private static readonly RmsDatabaseKind[] CanonicalDatabases =
    [
        RmsDatabaseKind.Branch,
        RmsDatabaseKind.Cashier
    ];

    private readonly IRmsDatabaseDiagnostics diagnostics;
    private readonly TimeProvider clock;
    private readonly TimeSpan timeout;

    public DatabaseHealthQueryHandler(
        IRmsDatabaseDiagnostics diagnostics,
        TimeProvider clock,
        TimeSpan? timeout = null)
    {
        this.diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.timeout = timeout ?? DefaultTimeout;
        if (this.timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }
    }

    public async Task<ApplicationResult<DatabaseHealthSnapshot>> HandleAsync(
        InvocationContext? context,
        CancellationToken cancellationToken = default)
    {
        var decision = AgentOperationAuthorization.Authorize(
            context,
            AgentOperationRisk.ReadOnlyDiagnostic);
        if (!decision.Allowed)
        {
            return ApplicationResult<DatabaseHealthSnapshot>.Failure(
                decision.Code,
                decision.Message);
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        try
        {
            var branchTask = diagnostics.DiagnoseAsync(
                RmsDatabaseKind.Branch,
                timeoutSource.Token);
            var cashierTask = diagnostics.DiagnoseAsync(
                RmsDatabaseKind.Cashier,
                timeoutSource.Token);
            var allDiagnostics = Task.WhenAll(branchTask, cashierTask);
            var results = await allDiagnostics
                .WaitAsync(timeoutSource.Token)
                .ConfigureAwait(false);

            if (!TryCreateSnapshot(results, out var snapshot))
            {
                return ApplicationResult<DatabaseHealthSnapshot>.Failure(
                    "database_health_unavailable",
                    "RMS database health is currently unavailable.");
            }

            return ApplicationResult<DatabaseHealthSnapshot>.Success(snapshot);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
        {
            return ApplicationResult<DatabaseHealthSnapshot>.Failure(
                "database_health_timeout",
                "Database health check timed out.");
        }
        catch
        {
            return ApplicationResult<DatabaseHealthSnapshot>.Failure(
                "database_health_unavailable",
                "RMS database health is currently unavailable.");
        }
    }

    public static DatabaseHealthOverallState DetermineOverallState(
        IReadOnlyList<DatabaseHealthItem> items)
    {
        if (items.Count != CanonicalDatabases.Length)
        {
            return DatabaseHealthOverallState.Unknown;
        }

        if (items.All(item => item.Status == RmsDatabaseDiagnosticStatus.Reachable))
        {
            return DatabaseHealthOverallState.Healthy;
        }

        if (items.All(item => item.Status is
            RmsDatabaseDiagnosticStatus.AuthenticationFailed
                or RmsDatabaseDiagnosticStatus.DatabaseUnavailable
                or RmsDatabaseDiagnosticStatus.Unreachable))
        {
            return DatabaseHealthOverallState.Unavailable;
        }

        return DatabaseHealthOverallState.Degraded;
    }

    private bool TryCreateSnapshot(
        IReadOnlyList<RmsDatabaseDiagnosticResult> results,
        out DatabaseHealthSnapshot snapshot)
    {
        snapshot = null!;
        if (results.Count != CanonicalDatabases.Length)
        {
            return false;
        }

        var items = new List<DatabaseHealthItem>(CanonicalDatabases.Length);
        foreach (var database in CanonicalDatabases)
        {
            var result = results.SingleOrDefault(candidate => candidate.Database == database);
            if (result is null || !TryCreateItem(result, database, out var item) || item is null)
            {
                return false;
            }

            items.Add(item);
        }

        snapshot = new(
            clock.GetUtcNow(),
            DetermineOverallState(items),
            items);
        return true;
    }

    private static bool TryCreateItem(
        RmsDatabaseDiagnosticResult result,
        RmsDatabaseKind expectedDatabase,
        out DatabaseHealthItem? item)
    {
        item = null;
        var definition = RmsDatabaseCatalog.For(expectedDatabase);
        if (result.Database != expectedDatabase
            || !string.Equals(result.ExpectedDatabase, definition.DatabaseName, StringComparison.OrdinalIgnoreCase)
            || !Enum.IsDefined(result.Status)
            || !IsSafeDisplayValue(result.ConfiguredDatabase, 128)
            || !IsSafeDisplayValue(result.ServerDisplay, 256)
            || result.CheckedAtUtc == default)
        {
            return false;
        }

        var requiresConfiguredData = result.Status is
            RmsDatabaseDiagnosticStatus.DatabaseNameMismatch
                or RmsDatabaseDiagnosticStatus.Reachable
                or RmsDatabaseDiagnosticStatus.AuthenticationFailed
                or RmsDatabaseDiagnosticStatus.DatabaseUnavailable
                or RmsDatabaseDiagnosticStatus.Unreachable;
        if (requiresConfiguredData
            && (!result.Configured
                || string.IsNullOrWhiteSpace(result.ConfiguredDatabase)
                || string.IsNullOrWhiteSpace(result.ServerDisplay)))
        {
            return false;
        }

        if (result.Status == RmsDatabaseDiagnosticStatus.NotConfigured && result.Configured)
        {
            return false;
        }

        if (result.Status == RmsDatabaseDiagnosticStatus.Reachable
            && result.DatabaseNameMatches != true)
        {
            return false;
        }

        if (result.Status == RmsDatabaseDiagnosticStatus.DatabaseNameMismatch
            && result.DatabaseNameMatches != false)
        {
            return false;
        }

        if (result.DatabaseNameMatches == true
            && string.IsNullOrWhiteSpace(result.ConfiguredDatabase))
        {
            return false;
        }

        if (result.DatabaseNameMatches == false
            && result.Status != RmsDatabaseDiagnosticStatus.DatabaseNameMismatch)
        {
            return false;
        }

        item = new(
            expectedDatabase,
            definition.DisplayName,
            definition.DatabaseName,
            result.ConfiguredDatabase,
            result.ServerDisplay,
            result.Configured,
            result.DatabaseNameMatches,
            result.Status,
            SafeDetailFor(result.Status),
            result.CheckedAtUtc);
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

    private static bool IsSafeDisplayValue(string? value, int maximumLength)
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
