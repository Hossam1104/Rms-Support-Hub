using RmsSupportHub.Pos.Application.Invocation;
using RmsSupportHub.Pos.Application.Services;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Application.Tests;

public sealed class RmsDatabaseBackupQueryHandlerTests
{
    private const string Principal = "S-1-5-21-1111111111-2222222222-3333333333-1001";

    [Theory]
    [InlineData(InvocationSource.LocalWpf, InvocationAuthorizationLevel.LocalAdministrator, true)]
    [InlineData(InvocationSource.LocalWpf, InvocationAuthorizationLevel.LocalOperator, false)]
    [InlineData(InvocationSource.LocalWpf, InvocationAuthorizationLevel.Unauthenticated, false)]
    [InlineData(InvocationSource.RemoteHub, InvocationAuthorizationLevel.RemoteAdministrator, false)]
    [InlineData(InvocationSource.AgentInternal, InvocationAuthorizationLevel.LocalAdministrator, false)]
    public async Task BackupCreationUsesOnlyTrustedLocalAdministratorAuthority(
        InvocationSource source,
        InvocationAuthorizationLevel authority,
        bool expectedAllowed)
    {
        var workflow = new RecordingWorkflow();
        var handler = new RmsDatabaseBackupQueryHandler(
            workflow,
            new EmptyStorage(),
            TimeProvider.System);
        var context = new InvocationContext(source, Principal, authority, "correlation-backup");

        var result = await handler.CreateAsync(context, RmsDatabaseKind.Branch);

        Assert.Equal(expectedAllowed, result.Succeeded);
        Assert.Equal(expectedAllowed ? "" : "administrator_authorization_required", result.Code);
        Assert.Equal(expectedAllowed ? RmsDatabaseKind.Branch : null, workflow.Database);
        Assert.Equal(expectedAllowed ? Principal : null, workflow.PrincipalSid);
    }

    [Fact]
    public async Task InventoryIsPrincipalScopedBoundedAndNewestFirstAcrossFixedTargets()
    {
        var storage = new InventoryStorage();
        var handler = new RmsDatabaseBackupQueryHandler(
            new RecordingWorkflow(),
            storage,
            TimeProvider.System);
        var context = new InvocationContext(
            InvocationSource.LocalWpf,
            Principal,
            InvocationAuthorizationLevel.LocalOperator,
            "correlation-inventory");

        var result = await handler.InventoryAsync(context);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Value);
        Assert.Equal(64, result.Value!.Items.Count);
        Assert.True(result.Value.Items[0].CreatedAtUtc >= result.Value.Items[1].CreatedAtUtc);
        Assert.Contains(result.Value.Items, item => item.Database == RmsDatabaseKind.Branch);
        Assert.Contains(result.Value.Items, item => item.Database == RmsDatabaseKind.Cashier);
        Assert.Contains(result.Value.Items, item => item.Availability == RmsDatabaseBackupAvailability.Expired);
        Assert.Equal(Principal, storage.LastPrincipalSid);
        Assert.DoesNotContain(result.Value.Items, item => item.DisplayName.Contains("\\", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Value.Items, item => item.DisplayName.Contains("/", StringComparison.Ordinal));
    }

    private sealed class RecordingWorkflow : IRmsDatabaseWorkflow
    {
        public RmsDatabaseKind? Database { get; private set; }

        public string? PrincipalSid { get; private set; }

        public Task<RmsDatabaseWorkflowResult> BackupAsync(
            RmsDatabaseKind database,
            IProgress<RmsDatabaseProgress>? progress = null,
            CancellationToken cancellationToken = default,
            string? principalSid = null)
        {
            Database = database;
            PrincipalSid = principalSid;
            return Task.FromResult(new RmsDatabaseWorkflowResult(
                RmsDatabaseWorkflowOutcome.Completed,
                "backup_completed",
                "completed",
                new RmsApprovedDatabaseBackup(
                    database,
                    "0123456789abcdef0123456789abcdef",
                    "backup.bak",
                    10,
                    new string('a', 64),
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow.AddDays(1),
                    "internal-path.bak",
                    PrincipalSid: principalSid),
                false,
                false,
                []));
        }

        public Task<RmsDatabaseWorkflowResult> RestoreAsync(
            RmsDatabaseKind database,
            string artifactId,
            IProgress<RmsDatabaseProgress>? progress = null,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Restore is not part of the WPF-06 local backup seam.");
    }

    private class EmptyStorage : IRmsDatabaseBackupStorage
    {
        public Task<RmsDatabaseBackupAllocation> AllocateAsync(RmsDatabaseKind database, DateTimeOffset createdAtUtc, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RmsApprovedDatabaseBackup?> RegisterAsync(RmsDatabaseKind database, RmsDatabaseBackupAllocation allocation, CancellationToken cancellationToken = default, string? principalSid = null) =>
            throw new NotSupportedException();

        public Task<RmsApprovedDatabaseBackup?> ResolveAsync(RmsDatabaseKind database, string artifactId, CancellationToken cancellationToken = default, string? principalSid = null) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<RmsApprovedDatabaseBackup>> ListAsync(RmsDatabaseKind database, CancellationToken cancellationToken = default, string? principalSid = null) =>
            throw new NotSupportedException();

        public virtual Task<IReadOnlyList<RmsApprovedDatabaseBackup>> ListInventoryAsync(RmsDatabaseKind database, string principalSid, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RmsApprovedDatabaseBackup>>([]);

        public Task<bool> RevokeAsync(RmsDatabaseKind database, string artifactId, string principalSid, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class InventoryStorage : EmptyStorage
    {
        private readonly DateTimeOffset start = new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);

        public string? LastPrincipalSid { get; private set; }

        public override Task<IReadOnlyList<RmsApprovedDatabaseBackup>> ListInventoryAsync(
            RmsDatabaseKind database,
            string principalSid,
            CancellationToken cancellationToken = default)
        {
            LastPrincipalSid = principalSid;
            var items = Enumerable.Range(0, 40)
                .Select(index => Create(database, index))
                .ToArray();
            return Task.FromResult<IReadOnlyList<RmsApprovedDatabaseBackup>>(items);
        }

        private RmsApprovedDatabaseBackup Create(RmsDatabaseKind database, int index)
        {
            var created = start.AddMinutes(-index);
            return new(
                database,
                $"{index:x32}",
                $"{database}-backup-{index}.bak",
                1024,
                new string('b', 64),
                created,
                created.AddDays(1),
                $"internal-{database}-{index}.bak",
                index == 3 ? RmsDatabaseBackupAvailability.Expired : RmsDatabaseBackupAvailability.Available,
                Principal);
        }
    }
}
