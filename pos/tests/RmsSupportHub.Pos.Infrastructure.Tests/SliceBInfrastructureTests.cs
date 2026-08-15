using RmsSupportHub.Pos.Application.Packages;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;
using RmsSupportHub.Pos.Infrastructure.Diagnostics;
using RmsSupportHub.Pos.Infrastructure.MainServer;
using RmsSupportHub.Pos.Infrastructure.Snapshots;
using RmsSupportHub.Pos.Infrastructure.Configuration;

namespace RmsSupportHub.Pos.Infrastructure.Tests;

public sealed class SliceBInfrastructureTests
{
    [Fact]
    public void MainServerProfilesRejectNonHttpsAndFreeformApiBases()
    {
        var options = new MainServerProfileOptions
        {
            TestingBaseAddress = "http://operator-selected.example/api"
        };

        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    [Fact]
    public void MainServerBindingRequiresTheExactServerOwnedEndpoint()
    {
        var catalog = new MainServerProfileCatalog(new MainServerProfileOptions
        {
            TestingBaseAddress = "https://main.synthetic.test/rmsmainserverApi/"
        });
        var installation = new RmsInstallationSnapshot(
            true,
            true,
            true,
            "BR-001",
            "POS-01",
            null,
            null,
            "installation-guid",
            "https://main.synthetic.test/rmsmainserverApi/",
            null,
            "RMS+ UPC",
            "5.7.4",
            new("5.7.4", "5.7.4", "5.7.4"),
            new(RmsConsistencyState.Consistent, RmsConsistencyState.Consistent, RmsConsistencyState.Consistent, RmsConsistencyState.Consistent, RmsConsistencyState.Consistent, []),
            new(true, RmsConnectionStringState.Valid, "sql.synthetic.test", "RmsBranchSrv", true),
            new(true, RmsConnectionStringState.Valid, "sql.synthetic.test", "RmsCashierSrv", true),
            new(RmsEndpointConfigurationState.Configured, "main.synthetic.test", 443, "https://main.synthetic.test/rmsmainserverApi/"),
            new(RmsEndpointConfigurationState.Unavailable, null, null, null),
            new(true, true, true, true));

        Assert.True(catalog.TryGetActive(installation, out _, out var bound));
        Assert.Equal(MainServerBindingState.Bound, bound.State);

        var mismatched = installation with
        {
            MainServerUrl = "https://main.synthetic.test/rmsmainserverApi/other"
        };
        Assert.False(catalog.TryGetActive(mismatched, out _, out var mismatch));
        Assert.Equal(MainServerBindingState.Mismatch, mismatch.State);
    }

    [Fact]
    public void PrivilegedMutationLeaseSerializesDifferentScopesAndPrincipals()
    {
        using var lease = new MachineWidePrivilegedMutationLease();

        var first = lease.TryAcquire("agent-package.upgrade", "S-1-5-21-first");
        if (first.State == PrivilegedMutationLeaseState.Unavailable)
        {
            // A non-elevated Windows test process is expected to fail closed
            // when it cannot open the Global semaphore. The elevated,
            // two-process proof covers the real cross-process property.
            Assert.True(OperatingSystem.IsWindows());
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            Assert.False(principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator), first.Detail);
            return;
        }

        Assert.Equal(PrivilegedMutationLeaseState.Acquired, first.State);
        Assert.NotNull(first.Handle);

        var second = lease.TryAcquire("repair.installation", "S-1-5-21-second");
        Assert.Equal(PrivilegedMutationLeaseState.Busy, second.State);

        first.Handle!.Dispose();
        var third = lease.TryAcquire("repair.installation", "S-1-5-21-second");
        Assert.Equal(PrivilegedMutationLeaseState.Acquired, third.State);
        third.Handle!.Dispose();
    }

    [Fact]
    public void PackagePolicyRejectsArchiveTraversalAndNonAgentServiceIdentity()
    {
        var manifest = new AgentPackageManifest(
            "agent-package",
            "1.0.0",
            "Windows",
            "net10.0-windows",
            "RMS+ POS Agent",
            "NetworkService",
            "RmsSupportHub.Pos.Agent",
            "SHA256withRSA",
            "DBS test signer",
            new string('a', 64),
            "signature-placeholder",
            [new AgentPackageFileManifest("agent", "..\\escape.dll", 0, new string('b', 64), true)],
            [],
            [],
            null,
            false,
            1);

        var result = new AgentPackagePolicy().ValidateManifest(manifest);

        Assert.Equal(AgentPackageVerificationState.Rejected, result.State);
        Assert.Contains("service_identity_invalid", result.Blockers);
        Assert.Contains("file_path_invalid", result.Blockers);
    }

    [Fact]
    public async Task SafetySnapshotsVerifyIntegrityAndPrincipalScope()
    {
        var root = Path.Combine(Path.GetTempPath(), "RmsSupportHub-SliceB", Guid.NewGuid().ToString("N"));
        try
        {
            var options = new SafetySnapshotOptions { RootDirectory = root, MaxSnapshots = 4, Lifetime = TimeSpan.FromHours(1) };
            var store = new FileSafetySnapshotStore(options);
            var snapshot = new SafetySnapshotDocument(
                "0123456789abcdef0123456789abcdef",
                "S-1-5-21-100-200-300-400",
                "Testing",
                "main-server-testing",
                "BR-001",
                "POS-01",
                "installation-guid",
                "RMS+",
                "2026.08",
                null,
                null,
                new string('c', 64),
                [new SafetySnapshotServiceEvidence("svc-branch", "running", SafetySnapshotEvidenceState.Healthy)],
                [new SafetySnapshotDatabaseEvidence("branch", "reachable", SafetySnapshotEvidenceState.Healthy, "RmsBranchSrv")],
                new SafetySnapshotCapacityEvidence(SafetySnapshotEvidenceState.Healthy, 1024 * 1024, "Fixed-root capacity."),
                new SafetySnapshotBackupEvidence(SafetySnapshotEvidenceState.Healthy, 1, DateTimeOffset.UtcNow, "Approved backup metadata."),
                SafetySnapshotEvidenceState.Healthy,
                "correlation",
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddHours(1),
                string.Empty);
            snapshot = snapshot with { IntegrityHash = FileSafetySnapshotStore.ComputeIntegrityHash(snapshot) };

            await store.SaveAsync(snapshot);

            var verified = await store.ReadAndVerifyAsync(snapshot.PrincipalSid, snapshot.SnapshotId, DateTimeOffset.UtcNow);
            var mismatched = await store.ReadAndVerifyAsync("S-1-5-21-other", snapshot.SnapshotId, DateTimeOffset.UtcNow);

            Assert.True(verified.Verified);
            Assert.Equal(SafetySnapshotState.Verified, verified.State);
            Assert.Equal(SafetySnapshotState.Mismatched, mismatched.State);
            Assert.False(mismatched.Verified);
        }
        finally
        {
            try { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [Fact]
    public void DiagnosticOutputRedactorRemovesCredentialsPathsSidsAndPrivateKeys()
    {
        var text = "password=secret token=abc C:\\ProgramData\\RMS\\agent.json S-1-5-21-1-2-3-4 -----BEGIN PRIVATE KEY-----secret-----END PRIVATE KEY-----";

        var redacted = new DiagnosticOutputRedactor().Redact(text);

        Assert.DoesNotContain("secret", redacted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ProgramData", redacted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("S-1-5-21", redacted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("BEGIN PRIVATE KEY", redacted, StringComparison.OrdinalIgnoreCase);
    }
}
