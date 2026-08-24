using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using RmsSupportHub.Pos.Agent.Artifacts;
using RmsSupportHub.Pos.Agent.IntegrationTests.TestSupport;
using RmsSupportHub.Pos.Agent.Support;
using RmsSupportHub.Pos.Application.Invocation;
using RmsSupportHub.Pos.Contracts.V1.LocalIpc;
using RmsSupportHub.Pos.Contracts.V1.Rms;
using RmsSupportHub.Pos.Domain.Exceptions;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;
using RmsSupportHub.Pos.Infrastructure.Backups;

namespace RmsSupportHub.Pos.Agent.IntegrationTests;

public sealed class Wpf06BackupArtifactDeliveryTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);
    private static readonly string PrincipalA =
        WindowsIdentity.GetCurrent().User?.Value
        ?? throw new InvalidOperationException("The test process does not have a Windows SID.");
    private const string PrincipalB = "S-1-5-21-1111111111-2222222222-3333333333-1002";
    private readonly string root = Directory.CreateTempSubdirectory("rms-wpf06-delivery-").FullName;

    [Fact]
    public async Task SamePrincipalDatabaseBackupExportsThroughSharedDeliveryService()
    {
        var clock = new ManualTimeProvider(Start);
        var fixture = CreateFixture(clock);
        var backup = await fixture.RegisterDatabaseBackupAsync(PrincipalA, "branch-backup-content");
        var destination = Path.Combine(fixture.DestinationRoot, "branch-export.bak");

        var result = await fixture.ExportAsync(
            Context(PrincipalA),
            PrincipalA,
            new(LocalIpcArtifactKind.DatabaseBackup, backup.ArtifactId, RmsDatabaseTarget.Branch, destination, false));

        Assert.Equal(LocalIpcArtifactExportState.Succeeded, result.State);
        Assert.Equal("branch-backup-content", await File.ReadAllTextAsync(destination));
        Assert.Equal(["accepted", "completed"], fixture.Audit.Events.Select(item => item.Outcome));
        Assert.All(fixture.Audit.Events, item => Assert.Equal("correlation-export", item.CorrelationId));
        Assert.DoesNotContain(fixture.Audit.Events, item => item.Source!.Contains(destination, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(fixture.BackupRoot, result.DisplayName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BackupLargerThanConfiguredCeilingIsDeletedAndRejected()
    {
        var clock = new ManualTimeProvider(Start);
        var fixture = CreateFixture(clock, maximumBackupBytes: 1);
        var displayName = "RmsBranchSrv_oversized.bak";
        var path = Path.Combine(fixture.BackupRoot, displayName);
        await File.WriteAllTextAsync(path, "too-large");
        var allocation = new RmsDatabaseBackupAllocation(path, displayName, Start);

        await Assert.ThrowsAsync<RmsDatabaseBackupSizeLimitException>(() => fixture.Storage.RegisterAsync(
            RmsDatabaseKind.Branch,
            allocation,
            PrincipalA,
            CancellationToken.None));

        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task PrincipalAndWindowsIdentityMismatchIsRejectedBeforeDestinationWork()
    {
        var fixture = CreateFixture(new ManualTimeProvider(Start));
        var backup = await fixture.RegisterDatabaseBackupAsync(PrincipalA, "private-backup");
        var destination = Path.Combine(fixture.DestinationRoot, "mismatch.bak");

        var result = await fixture.ExportAsync(
            Context(PrincipalB),
            PrincipalB,
            new(LocalIpcArtifactKind.DatabaseBackup, backup.ArtifactId, RmsDatabaseTarget.Branch, destination, false));

        Assert.Equal(LocalIpcArtifactExportState.Unauthorized, result.State);
        Assert.Equal("unauthorized", result.ErrorCode);
        Assert.False(File.Exists(destination));
        Assert.Empty(fixture.Audit.Events);
    }

    [Fact]
    public async Task MissingCallerIdentityIsRejectedBeforeDestinationWork()
    {
        var fixture = CreateFixture(new ManualTimeProvider(Start));
        var backup = await fixture.RegisterDatabaseBackupAsync(PrincipalA, "private-backup");
        var destination = Path.Combine(fixture.DestinationRoot, "missing-identity.bak");

        var result = await fixture.Delivery.ExportAsync(
            Context(PrincipalA),
            PrincipalA,
            new(LocalIpcArtifactKind.DatabaseBackup, backup.ArtifactId, RmsDatabaseTarget.Branch, destination, false),
            null);

        Assert.Equal(LocalIpcArtifactExportState.Unauthorized, result.State);
        Assert.Equal("unauthorized", result.ErrorCode);
        Assert.False(File.Exists(destination));
        Assert.Empty(fixture.Audit.Events);
    }

    [Fact]
    public async Task WrongPrincipalGetsSafeNotFoundAndCannotExportAnotherPrincipalArtifact()
    {
        var fixture = CreateFixture(new ManualTimeProvider(Start));
        var backup = await fixture.RegisterDatabaseBackupAsync(PrincipalB, "private-backup");
        var destination = Path.Combine(fixture.DestinationRoot, "wrong-principal.bak");

        var result = await fixture.ExportAsync(
            Context(PrincipalA),
            PrincipalA,
            new(LocalIpcArtifactKind.DatabaseBackup, backup.ArtifactId, RmsDatabaseTarget.Branch, destination, false));

        Assert.Equal(LocalIpcArtifactExportState.ArtifactNotFound, result.State);
        Assert.Equal("artifact_not_found", result.ErrorCode);
        Assert.False(File.Exists(destination));
        Assert.Empty(fixture.Audit.Events);
    }

    [Fact]
    public async Task ExpiredAndTamperedBackupsAreRejectedWithTruthfulSafeStates()
    {
        var clock = new ManualTimeProvider(Start);
        var fixture = CreateFixture(clock, retention: TimeSpan.FromHours(1));
        var expired = await fixture.RegisterDatabaseBackupAsync(PrincipalA, "expired-backup");
        clock.Advance(TimeSpan.FromHours(1));

        var expiredResult = await fixture.ExportAsync(
            Context(PrincipalA),
            PrincipalA,
            new(LocalIpcArtifactKind.DatabaseBackup, expired.ArtifactId, RmsDatabaseTarget.Branch, Path.Combine(fixture.DestinationRoot, "expired.bak"), false));

        Assert.Equal(LocalIpcArtifactExportState.ArtifactExpired, expiredResult.State);

        clock = new ManualTimeProvider(Start);
        fixture = CreateFixture(clock);
        var tampered = await fixture.RegisterDatabaseBackupAsync(PrincipalA, "0123456789");
        await File.WriteAllTextAsync(tampered.ServerPath, "tampered!!");

        var tamperedResult = await fixture.ExportAsync(
            Context(PrincipalA),
            PrincipalA,
            new(LocalIpcArtifactKind.DatabaseBackup, tampered.ArtifactId, RmsDatabaseTarget.Branch, Path.Combine(fixture.DestinationRoot, "tampered.bak"), false));

        Assert.Equal(LocalIpcArtifactExportState.ChecksumMismatch, tamperedResult.State);
        Assert.Equal("artifact_checksum_mismatch", tamperedResult.ErrorCode);
    }

    [Fact]
    public async Task AuditFailurePreventsExportAndLeavesDestinationUntouched()
    {
        var fixture = CreateFixture(new ManualTimeProvider(Start), auditResult: false);
        var backup = await fixture.RegisterDatabaseBackupAsync(PrincipalA, "audit-failure");
        var destination = Path.Combine(fixture.DestinationRoot, "audit-failure.bak");

        var result = await fixture.ExportAsync(
            Context(PrincipalA),
            PrincipalA,
            new(LocalIpcArtifactKind.DatabaseBackup, backup.ArtifactId, RmsDatabaseTarget.Branch, destination, false));

        Assert.Equal(LocalIpcArtifactExportState.Failed, result.State);
        Assert.Equal("audit_unavailable", result.ErrorCode);
        Assert.False(File.Exists(destination));
        Assert.Single(fixture.Audit.Events);
    }

    [Fact]
    public async Task ExistingDestinationRequiresConfirmationAndConfirmedOverwriteIsAtomic()
    {
        var fixture = CreateFixture(new ManualTimeProvider(Start));
        var backup = await fixture.RegisterDatabaseBackupAsync(PrincipalA, "new-backup");
        var destination = Path.Combine(fixture.DestinationRoot, "existing.bak");
        await File.WriteAllTextAsync(destination, "old-content");

        var rejected = await fixture.ExportAsync(
            Context(PrincipalA),
            PrincipalA,
            new(LocalIpcArtifactKind.DatabaseBackup, backup.ArtifactId, RmsDatabaseTarget.Branch, destination, false));
        Assert.Equal(LocalIpcArtifactExportState.DestinationExists, rejected.State);
        Assert.Equal("old-content", await File.ReadAllTextAsync(destination));

        var accepted = await fixture.ExportAsync(
            Context(PrincipalA),
            PrincipalA,
            new(LocalIpcArtifactKind.DatabaseBackup, backup.ArtifactId, RmsDatabaseTarget.Branch, destination, true));
        Assert.Equal(LocalIpcArtifactExportState.Succeeded, accepted.State);
        Assert.Equal("new-backup", await File.ReadAllTextAsync(destination));
        Assert.DoesNotContain(Directory.EnumerateFiles(fixture.DestinationRoot), path => Path.GetFileName(path).EndsWith(".tmp", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ConcurrentExportsToTheSameDestinationFailClosed()
    {
        var audit = new RecordingAuditSink
        {
            Result = true,
            Entered = new(TaskCreationOptions.RunContinuationsAsynchronously),
            Release = new(TaskCreationOptions.RunContinuationsAsynchronously)
        };
        var fixture = CreateFixture(new ManualTimeProvider(Start), auditSink: audit);
        var backup = await fixture.RegisterDatabaseBackupAsync(PrincipalA, "same-destination");
        var destination = Path.Combine(fixture.DestinationRoot, "same-destination.bak");
        var request = new LocalIpcArtifactExportRequestDto(
            LocalIpcArtifactKind.DatabaseBackup,
            backup.ArtifactId,
            RmsDatabaseTarget.Branch,
            destination,
            false);

        var firstTask = Task.Run(() => fixture.ExportAsync(Context(PrincipalA), PrincipalA, request));
        await audit.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var second = await fixture.ExportAsync(Context(PrincipalA), PrincipalA, request);
        Assert.Equal(LocalIpcArtifactExportState.Failed, second.State);
        Assert.Equal("operation_in_progress", second.ErrorCode);

        audit.Release.TrySetResult(true);
        var first = await firstTask;
        Assert.Equal(LocalIpcArtifactExportState.Succeeded, first.State);
        Assert.Equal("same-destination", await File.ReadAllTextAsync(destination));
    }

    [Fact]
    public async Task SupportBundleUsesTheSameDeliveryServiceAndZipExtensionPolicy()
    {
        var fixture = CreateFixture(new ManualTimeProvider(Start));
        var sourcePath = Path.Combine(fixture.SourceRoot, "support.zip");
        await File.WriteAllTextAsync(sourcePath, "support-bundle");
        var bytes = await File.ReadAllBytesAsync(sourcePath);
        var metadata = fixture.Artifacts.Register(
            PrincipalA,
            "rms-support-bundle.zip",
            sourcePath,
            bytes.LongLength,
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
            Start);
        var destination = Path.Combine(fixture.DestinationRoot, "support-export.zip");

        var result = await fixture.ExportAsync(
            Context(PrincipalA),
            PrincipalA,
            new(LocalIpcArtifactKind.SupportBundle, metadata.ArtifactId, null, destination, false));

        Assert.Equal(LocalIpcArtifactExportState.Succeeded, result.State);
        Assert.Equal("support-bundle", await File.ReadAllTextAsync(destination));
        Assert.Contains(fixture.Audit.Events, audit => audit.Target == "support-bundle");
    }

    [Fact]
    public async Task FinalCompletedAuditFailureDoesNotClaimSuccessAndRestoresOverwrite()
    {
        var audit = new RecordingAuditSink { Results = new Queue<bool>([true, false]) };
        var fixture = CreateFixture(new ManualTimeProvider(Start), auditSink: audit);
        var backup = await fixture.RegisterDatabaseBackupAsync(PrincipalA, "new-backup");
        var destination = Path.Combine(fixture.DestinationRoot, "rollback.bak");
        await File.WriteAllTextAsync(destination, "old-content");

        var result = await fixture.ExportAsync(
            Context(PrincipalA),
            PrincipalA,
            new(LocalIpcArtifactKind.DatabaseBackup, backup.ArtifactId, RmsDatabaseTarget.Branch, destination, true));

        Assert.Equal(LocalIpcArtifactExportState.Failed, result.State);
        Assert.Equal("audit_unavailable", result.ErrorCode);
        Assert.Equal("old-content", await File.ReadAllTextAsync(destination));
        Assert.DoesNotContain(Directory.EnumerateFiles(fixture.DestinationRoot), path =>
            Path.GetFileName(path).Contains("rollback", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(path, destination, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CancellationRecordsCancelledOutcomeWithoutWritingOutput()
    {
        var fixture = CreateFixture(new ManualTimeProvider(Start));
        var sourcePath = Path.Combine(fixture.SourceRoot, "support.zip");
        await File.WriteAllTextAsync(sourcePath, "support-bundle");
        var bytes = await File.ReadAllBytesAsync(sourcePath);
        var metadata = fixture.Artifacts.Register(
            PrincipalA,
            "rms-support-bundle.zip",
            sourcePath,
            bytes.LongLength,
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
            Start);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var destination = Path.Combine(fixture.DestinationRoot, "cancelled.zip");
        var result = await fixture.ExportAsync(
            Context(PrincipalA),
            PrincipalA,
            new(LocalIpcArtifactKind.SupportBundle, metadata.ArtifactId, null, destination, false),
            cancellation.Token);

        Assert.Equal(LocalIpcArtifactExportState.Cancelled, result.State);
        Assert.Equal(["accepted", "cancelled"], fixture.Audit.Events.Select(item => item.Outcome));
        Assert.False(File.Exists(destination));
    }

    [Fact]
    public async Task RequestValidationRejectsContradictoryArtifactTargetFields()
    {
        var fixture = CreateFixture(new ManualTimeProvider(Start));
        var backup = await fixture.RegisterDatabaseBackupAsync(PrincipalA, "backup");

        var supportWithTarget = await fixture.ExportAsync(
            Context(PrincipalA),
            PrincipalA,
            new(LocalIpcArtifactKind.SupportBundle, backup.ArtifactId, RmsDatabaseTarget.Branch, Path.Combine(fixture.DestinationRoot, "support.zip"), false));
        var backupWithoutTarget = await fixture.ExportAsync(
            Context(PrincipalA),
            PrincipalA,
            new(LocalIpcArtifactKind.DatabaseBackup, backup.ArtifactId, null, Path.Combine(fixture.DestinationRoot, "backup.bak"), false));
        var undefinedTarget = await fixture.ExportAsync(
            Context(PrincipalA),
            PrincipalA,
            new(LocalIpcArtifactKind.DatabaseBackup, backup.ArtifactId, (RmsDatabaseTarget)99, Path.Combine(fixture.DestinationRoot, "undefined.bak"), false));

        Assert.All(new[] { supportWithTarget, backupWithoutTarget, undefinedTarget }, result =>
        {
            Assert.Equal(LocalIpcArtifactExportState.DestinationRejected, result.State);
            Assert.Equal("invalid_request", result.ErrorCode);
        });
        Assert.Empty(fixture.Audit.Events);
    }

    [Fact]
    public void DestinationCoordinationRemovesIdleKeysAndAllowsDifferentDestinations()
    {
        var coordinator = new BoundedKeyedMutationCoordinator();
        Assert.True(coordinator.TryEnter("same.bak", out var first));
        Assert.False(coordinator.TryEnter("same.bak", out _));
        Assert.True(coordinator.TryEnter("different.bak", out var different));
        different.Dispose();
        first.Dispose();

        for (var index = 0; index < 2_000; index++)
        {
            Assert.True(coordinator.TryEnter($"unique-{index}.bak", out var lease));
            lease.Dispose();
        }

        Assert.Equal(0, coordinator.ActiveKeyCount);
    }

    [Theory]
    [InlineData("relative.bak")]
    [InlineData("\\\\server\\share\\backup.bak")]
    [InlineData("backup.bak:stream")]
    [InlineData("..\\outside.bak")]
    [InlineData("backup.zip")]
    [InlineData("CON.bak")]
    public void DestinationPolicyRejectsUnsafeOrWrongKindPaths(string path)
    {
        var destinationRoot = Path.Combine(root, "Destinations");
        Directory.CreateDirectory(destinationRoot);
        var policy = new LocalArtifactDestinationPolicy([destinationRoot]);
        var candidate = path.StartsWith("\\", StringComparison.Ordinal) || path.Contains(':') || path.StartsWith("relative", StringComparison.Ordinal)
            ? path
            : Path.Combine(destinationRoot, path);

        Assert.False(policy.TryValidate(candidate, ".bak", out _));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private InvocationContext Context(string principal) =>
        new(InvocationSource.LocalWpf, principal, InvocationAuthorizationLevel.LocalAdministrator, "correlation-export");

    private static async Task<WindowsIdentity> CreateCallerIdentityAsync()
    {
        var currentSid = WindowsIdentity.GetCurrent().User
            ?? throw new InvalidOperationException("The test process does not have a Windows SID.");
        var pipeName = "RmsWpf06.Identity." + Guid.NewGuid().ToString("N");
        var security = new PipeSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new PipeAccessRule(currentSid, PipeAccessRights.FullControl, AccessControlType.Allow));
        using var server = NamedPipeServerStreamAcl.Create(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            0,
            0,
            security);
        var waitForConnection = server.WaitForConnectionAsync();
        using var client = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous,
            TokenImpersonationLevel.Impersonation,
            HandleInheritability.None);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await client.ConnectAsync(timeout.Token);
        await waitForConnection;
        WindowsIdentity? identity = null;
        server.RunAsClient(() => identity = WindowsIdentity.GetCurrent());
        return identity ?? throw new InvalidOperationException("The test pipe did not yield a caller identity.");
    }

    private Fixture CreateFixture(
        TimeProvider clock,
        TimeSpan? retention = null,
        long? maximumBackupBytes = null,
        bool auditResult = true,
        RecordingAuditSink? auditSink = null)
    {
        var sourceRoot = Path.Combine(root, "Sources");
        var destinationRoot = Path.Combine(root, "Destinations");
        var backupRoot = Path.Combine(root, "AgentBackups");
        Directory.CreateDirectory(sourceRoot);
        Directory.CreateDirectory(destinationRoot);
        Directory.CreateDirectory(backupRoot);
        var fileSystem = new PhysicalBackupFileSystem();
        var options = new RmsDatabaseStorageOptions
        {
            BackupRootPath = backupRoot,
            DatabaseFilesRootPath = Path.Combine(root, "DatabaseFiles"),
            BackupRetention = retention ?? TimeSpan.FromDays(30),
            MaximumBackupBytes = maximumBackupBytes ?? 512L * 1024 * 1024
        };
        var catalog = new RmsDatabaseBackupCatalog(fileSystem, options, clock);
        var storage = new RmsDatabaseBackupStorage(fileSystem, catalog, options, clock);
        var artifacts = new ArtifactCatalog(fileSystem, clock, new RuntimeRetentionPolicy { ArtifactLifetime = TimeSpan.FromDays(30) });
        var audit = auditSink ?? new RecordingAuditSink { Result = auditResult };
        var delivery = new ArtifactDeliveryService(
            artifacts,
            storage,
            fileSystem,
            new LocalArtifactDestinationPolicy([destinationRoot]),
            new WindowsArtifactDestinationAuthority(),
            new BoundedKeyedMutationCoordinator(),
            audit,
            new SupportBundleOptions { BundleRootPath = sourceRoot },
            options,
            clock);
        return new Fixture(sourceRoot, destinationRoot, backupRoot, artifacts, storage, delivery, audit, clock);
    }

    private sealed class Fixture(
        string sourceRoot,
        string destinationRoot,
        string backupRoot,
        ArtifactCatalog artifacts,
        RmsDatabaseBackupStorage storage,
        ArtifactDeliveryService delivery,
        RecordingAuditSink audit,
        TimeProvider clock)
    {
        public string SourceRoot => sourceRoot;
        public string DestinationRoot => destinationRoot;
        public string BackupRoot => backupRoot;
        public ArtifactCatalog Artifacts => artifacts;
        public RmsDatabaseBackupStorage Storage => storage;
        public ArtifactDeliveryService Delivery => delivery;
        public RecordingAuditSink Audit => audit;

        public async Task<LocalIpcArtifactExportResultDto> ExportAsync(
            InvocationContext context,
            string principalSid,
            LocalIpcArtifactExportRequestDto request,
            CancellationToken cancellationToken = default)
        {
            using var callerIdentity = await CreateCallerIdentityAsync();
            return await Delivery.ExportAsync(context, principalSid, request, callerIdentity, cancellationToken);
        }

        public async Task<RmsApprovedDatabaseBackup> RegisterDatabaseBackupAsync(string principal, string contents)
        {
            var displayName = $"RmsBranchSrv_{Guid.NewGuid():N}.bak";
            var path = Path.Combine(backupRoot, displayName);
            await File.WriteAllTextAsync(path, contents);
            var allocation = new RmsDatabaseBackupAllocation(path, displayName, clock.GetUtcNow());
            return (await storage.RegisterAsync(RmsDatabaseKind.Branch, allocation, principal, CancellationToken.None))!;
        }
    }

    private sealed class RecordingAuditSink : IAgentAuditSink
    {
        public bool Result { get; set; }
        public Queue<bool>? Results { get; init; }
        public TaskCompletionSource<bool>? Entered { get; init; }
        public TaskCompletionSource<bool>? Release { get; init; }
        public List<AgentAuditEvent> Events { get; } = [];

        public bool Record(AgentAuditEvent auditEvent)
        {
            Events.Add(auditEvent);
            Entered?.TrySetResult(true);
            Release?.Task.GetAwaiter().GetResult();
            return Results is { Count: > 0 } ? Results.Dequeue() : Result;
        }
    }
}
