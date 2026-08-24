using RmsSupportHub.Pos.Application.Invocation;
using RmsSupportHub.Pos.Domain.Exceptions;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Application.Services;

/// <summary>
/// Transport-neutral WPF backup seam. Target resolution, authority, and artifact scope are kept
/// here or below the Agent composition boundary; no Contracts type or caller-selected SQL/path can
/// enter the application layer.
/// </summary>
public sealed class RmsDatabaseBackupQueryHandler(
    IRmsDatabaseWorkflow workflow,
    IRmsDatabaseBackupStorage storage,
    TimeProvider timeProvider)
{
    public async Task<RmsDatabaseApplicationResult<RmsDatabaseWorkflowResult>> CreateAsync(
        InvocationContext context,
        RmsDatabaseKind database,
        IProgress<RmsDatabaseProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var decision = AgentOperationAuthorization.Authorize(
            context,
            AgentOperationRisk.AdministratorOnlyMutation);
        if (!decision.Allowed)
        {
            return RmsDatabaseApplicationResult<RmsDatabaseWorkflowResult>.Failure(
                decision.Code,
                decision.Message);
        }

        try
        {
            var result = await workflow
                .BackupAsync(database, context.AuthenticatedCaller, progress, cancellationToken)
                .ConfigureAwait(false);
            return RmsDatabaseApplicationResult<RmsDatabaseWorkflowResult>.Success(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (RmsDatabaseAuditUnavailableException)
        {
            return RmsDatabaseApplicationResult<RmsDatabaseWorkflowResult>.Failure(
                "audit_unavailable",
                "The RMS database backup was not dispatched because required audit recording is unavailable.");
        }
        catch
        {
            return RmsDatabaseApplicationResult<RmsDatabaseWorkflowResult>.Failure(
                "backup_failed",
                "The RMS database backup could not be completed.");
        }
    }

    public async Task<RmsDatabaseApplicationResult<RmsDatabaseInventoryProjection>> InventoryAsync(
        InvocationContext context,
        CancellationToken cancellationToken = default)
    {
        var decision = AgentOperationAuthorization.Authorize(
            context,
            AgentOperationRisk.ReadOnlyDiagnostic);
        if (!decision.Allowed)
        {
            return RmsDatabaseApplicationResult<RmsDatabaseInventoryProjection>.Failure(
                decision.Code,
                decision.Message);
        }

        try
        {
            var items = new List<RmsDatabaseInventoryItem>();
            foreach (var target in RmsDatabaseCatalog.Definitions)
            {
                var backups = await storage
                    .ListInventoryAsync(target.Kind, context.AuthenticatedCaller, cancellationToken)
                    .ConfigureAwait(false);
                items.AddRange(backups.Select(backup => new RmsDatabaseInventoryItem(
                    target.Kind,
                    backup.ArtifactId,
                    backup.DisplayName,
                    backup.SizeBytes,
                    backup.Sha256Checksum,
                    backup.CreatedAtUtc,
                    backup.ExpiresAtUtc,
                    backup.Availability)));
            }

            var bounded = items
                .OrderByDescending(item => item.CreatedAtUtc)
                .Take(64)
                .ToArray();
            return RmsDatabaseApplicationResult<RmsDatabaseInventoryProjection>.Success(
                new(timeProvider.GetUtcNow(), bounded));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return RmsDatabaseApplicationResult<RmsDatabaseInventoryProjection>.Failure(
                "backup_inventory_unavailable",
                "The approved backup inventory is currently unavailable.");
        }
    }
}

public sealed record RmsDatabaseInventoryProjection(
    DateTimeOffset CheckedAtUtc,
    IReadOnlyList<RmsDatabaseInventoryItem> Items);

public sealed record RmsDatabaseInventoryItem(
    RmsDatabaseKind Database,
    string ArtifactId,
    string DisplayName,
    long SizeBytes,
    string Sha256Checksum,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ExpiresAtUtc,
    RmsDatabaseBackupAvailability Availability);

public sealed record RmsDatabaseApplicationResult<T>(
    bool Succeeded,
    T? Value,
    string Code,
    string Detail)
{
    public static RmsDatabaseApplicationResult<T> Success(T value) =>
        new(true, value, string.Empty, string.Empty);

    public static RmsDatabaseApplicationResult<T> Failure(string code, string detail) =>
        new(false, default, code, detail);
}
