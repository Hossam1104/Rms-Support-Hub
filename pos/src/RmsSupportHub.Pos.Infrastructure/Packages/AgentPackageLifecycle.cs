using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using RmsSupportHub.Pos.Application.Packages;
using RmsSupportHub.Pos.Domain.Enums;
using RmsSupportHub.Pos.Domain.Models;
using RmsSupportHub.Pos.Infrastructure.Configuration;
using RmsSupportHub.Pos.Infrastructure.Windows;

namespace RmsSupportHub.Pos.Infrastructure.Packages;

public interface IAgentPackageInstallationPlatform
{
    Task<bool> PrepareAsync(AgentPackageManifest manifest, CancellationToken cancellationToken = default);

    Task<bool> ActivateAsync(string stagedRoot, AgentPackageManifest manifest, CancellationToken cancellationToken = default);

    Task<bool> VerifyHealthAsync(AgentPackageManifest manifest, CancellationToken cancellationToken = default);

    // Rollback never takes the failed operation's incoming manifest as a trust or identity input.
    // The platform independently resolves which retained slot (rollback vs. recovery) and which
    // durable checkpoint identity to restore, based only on the operation that failed.
    Task<bool> RollbackAsync(AgentPackageOperationKind failedOperation, CancellationToken cancellationToken = default);

    // This overload preserves the original test seam while giving the production platform the
    // operation kind needed for install/upgrade/repair/uninstall transition rules.
    Task<bool> PrepareAsync(AgentPackageManifest manifest, AgentPackageOperationKind operation, CancellationToken cancellationToken = default) => PrepareAsync(manifest, cancellationToken);

    Task<bool> ActivateAsync(string stagedRoot, AgentPackageManifest manifest, AgentPackageOperationKind operation, CancellationToken cancellationToken = default) => ActivateAsync(stagedRoot, manifest, cancellationToken);
}

public interface IAgentHealthProbe
{
    Task<bool> ProbeAsync(CancellationToken cancellationToken = default);
}

public sealed class LoopbackAgentHealthProbe : IAgentHealthProbe
{
    public async Task<bool> ProbeAsync(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows()) return false;

        using var handler = new HttpClientHandler { AllowAutoRedirect = false };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
        foreach (var path in new[] { "/health/live", "/health/ready" })
        {
            using var response = await client.GetAsync(
                new Uri("https://rms-pos-agent.localhost:5001" + path),
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.OK) return false;
        }

        return true;
    }
}

/// <summary>
/// Real Windows activation platform. Every mutation is tied to the fixed Agent identity, a
/// verified installed manifest, a durable checkpoint, and the typed SCM/certificate seams.
/// </summary>
public sealed class WindowsAgentPackageInstallationPlatform : IAgentPackageInstallationPlatform
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly AgentPackageOptions options;
    private readonly IAgentPackageVerifier verifier;
    private readonly IAgentServiceLifecycleController serviceController;
    private readonly IAgentCertificatePrerequisite certificatePrerequisite;
    private readonly AgentPackageLifecycleStateStore stateStore;
    private readonly IAgentHealthProbe healthProbe;

    public WindowsAgentPackageInstallationPlatform()
        : this(
            new AgentPackageOptions(),
            new FileAgentPackageVerifier(
                new AgentPackageOptions(),
                new AgentPackagePolicy(),
                new MachineCertificatePackageSignatureVerifier()),
            new WindowsAgentServiceController(),
            new MachineAgentCertificatePrerequisite(),
            new AgentPackageLifecycleStateStore(new AgentPackageOptions()),
            new LoopbackAgentHealthProbe())
    {
    }

    public WindowsAgentPackageInstallationPlatform(
        AgentPackageOptions options,
        IAgentPackageVerifier verifier,
        IAgentServiceLifecycleController serviceController,
        IAgentCertificatePrerequisite certificatePrerequisite,
        AgentPackageLifecycleStateStore stateStore,
        IAgentHealthProbe healthProbe)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.verifier = verifier ?? throw new ArgumentNullException(nameof(verifier));
        this.serviceController = serviceController ?? throw new ArgumentNullException(nameof(serviceController));
        this.certificatePrerequisite = certificatePrerequisite ?? throw new ArgumentNullException(nameof(certificatePrerequisite));
        this.stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        this.healthProbe = healthProbe ?? throw new ArgumentNullException(nameof(healthProbe));
    }

    public string LastFailureCode { get; private set; } = "none";

    public Task<bool> PrepareAsync(AgentPackageManifest manifest, CancellationToken cancellationToken = default) =>
        PrepareAsync(manifest, AgentPackageOperationKind.Install, cancellationToken);

    public async Task<bool> PrepareAsync(
        AgentPackageManifest manifest,
        AgentPackageOperationKind operation,
        CancellationToken cancellationToken = default)
    {
        LastFailureCode = "none";
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            options.EnsureStorageProvisioned();
            if (!string.Equals(manifest.ReleaseChannel, options.ReleaseChannel, StringComparison.Ordinal)) return Fail("package_channel_not_configured");
            if (stateStore.HasUnreadableState) return Fail("recovery_required");
            var checkpoint = stateStore.Read();
            if (checkpoint is not null)
            {
                // A checkpoint left in Prepared/Staged never reached a destructive mutation of the
                // live installation, so it is safe to abandon and clear automatically. Anything at or
                // past Activated -- or already flagged RecoveryRequired -- stays fail-closed: only a
                // verified rollback/recovery outcome may resolve it.
                var safeToAutoClear = !checkpoint.RecoveryRequired
                    && checkpoint.Phase is AgentPackageLifecyclePhase.Prepared or AgentPackageLifecyclePhase.Staged;
                if (!safeToAutoClear) return Fail("recovery_required");
                stateStore.Clear();
            }

            var installed = await ReadInstalledManifestAsync(cancellationToken).ConfigureAwait(false);
            var service = serviceController.ReadPermanent();
            if (service is not null
                && (installed is null || !await IsOwnedServiceAsync(service, installed, cancellationToken).ConfigureAwait(false)))
            {
                return Fail("service_ownership_conflict");
            }

            if (operation == AgentPackageOperationKind.Uninstall)
            {
                return installed is null || await VerifyCurrentInstallAsync(installed, cancellationToken).ConfigureAwait(false);
            }

            if (operation == AgentPackageOperationKind.Health) return installed is not null;

            if (installed is not null)
            {
                if (!await VerifyCurrentInstallAsync(installed, cancellationToken).ConfigureAwait(false)) return false;
                if (!ValidateTransition(operation, installed.Version, manifest.Version, out var transitionFailure)) return Fail(transitionFailure);
            }
            else if (operation is AgentPackageOperationKind.Upgrade or AgentPackageOperationKind.Repair or AgentPackageOperationKind.Rollback)
            {
                return Fail("installed_package_missing");
            }
            else if (Directory.EnumerateFileSystemEntries(options.InstallationRoot).Any())
            {
                return Fail("installation_ownership_unproven");
            }

            if (!certificatePrerequisite.IsSatisfied(manifest, out var certificateFailure))
            {
                return Fail(certificateFailure);
            }

            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Fail("platform_preparation_unknown");
        }
    }

    public Task<bool> ActivateAsync(string stagedRoot, AgentPackageManifest manifest, CancellationToken cancellationToken = default) =>
        ActivateAsync(stagedRoot, manifest, AgentPackageOperationKind.Install, cancellationToken);

    public async Task<bool> ActivateAsync(
        string stagedRoot,
        AgentPackageManifest manifest,
        AgentPackageOperationKind operation,
        CancellationToken cancellationToken = default)
    {
        LastFailureCode = "none";
        if (operation == AgentPackageOperationKind.Uninstall) return await UninstallAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.Equals(manifest.ReleaseChannel, options.ReleaseChannel, StringComparison.Ordinal)) return Fail("package_channel_not_configured");
            if (!OperatingSystem.IsWindows()) return Fail("windows_platform_required");
            if (!IsSafeStagingRoot(stagedRoot) || !Directory.Exists(stagedRoot)) return Fail("staging_root_invalid");
            if (!await VerifyStagedRootAsync(stagedRoot, manifest, cancellationToken).ConfigureAwait(false)) return Fail("staged_package_mismatch");

            var installed = await ReadInstalledManifestAsync(cancellationToken).ConfigureAwait(false);
            var service = serviceController.ReadPermanent();
            if (service is not null
                && (installed is null || !await IsOwnedServiceAsync(service, installed, cancellationToken).ConfigureAwait(false)))
            {
                return Fail("service_ownership_conflict");
            }

            var now = DateTimeOffset.UtcNow;
            var operationId = Guid.NewGuid().ToString("N");
            stateStore.Write(new(operationId, operation, AgentPackageLifecyclePhase.Prepared, manifest.PackageId, manifest.Version, installed?.Version, now, now, null, false));
            await WriteManifestAsync(stagedRoot, manifest, cancellationToken).ConfigureAwait(false);
            stateStore.Write(ReadState(operationId, operation, AgentPackageLifecyclePhase.Staged, manifest, installed));

            if (installed is not null)
            {
                // Upgrade/Repair/Install-over-existing retain the PREVIOUS package so a failed
                // activation can restore it. An explicit Rollback instead retains the CURRENT
                // package into a separate bounded recovery slot, so a failed rollback can restore
                // the version it was rolling back from instead of retrying the same failed target.
                var preserved = operation == AgentPackageOperationKind.Rollback
                    ? await PreserveRecoveryAsync(installed, cancellationToken).ConfigureAwait(false)
                    : await PreserveRollbackAsync(installed, cancellationToken).ConfigureAwait(false);
                if (!preserved) return FailAndCheckpoint(operation == AgentPackageOperationKind.Rollback ? "recovery_preservation_failed" : "rollback_preservation_failed");
                stateStore.Write(ReadState(operationId, operation, AgentPackageLifecyclePhase.PreviousPreserved, manifest, installed));
            }

            if (installed is not null && !StopOwnedServiceBeforeReplacement(cancellationToken)) return FailAndCheckpoint("owned_service_stop_failed");

            if (!ReplaceInstallation(stagedRoot, cancellationToken)) return FailAndCheckpoint("installation_activation_failed");
            stateStore.Write(ReadState(operationId, operation, AgentPackageLifecyclePhase.Activated, manifest, installed));

            var executable = GetExecutablePath(manifest);
            if (executable is null || !serviceController.EnsureConfigured(executable)) return FailAndCheckpoint("service_configuration_failed");
            if (!serviceController.Start(cancellationToken)) return FailAndCheckpoint("owned_service_start_failed");
            stateStore.Write(ReadState(operationId, operation, AgentPackageLifecyclePhase.HealthChecking, manifest, installed));
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            LastFailureCode = "activation_cancelled";
            return false;
        }
        catch
        {
            return FailAndCheckpoint("activation_unknown");
        }
    }

    public async Task<bool> VerifyHealthAsync(AgentPackageManifest manifest, CancellationToken cancellationToken = default)
    {
        try
        {
            var installed = await ReadInstalledManifestAsync(cancellationToken).ConfigureAwait(false);
            if (installed is null || !ManifestEquivalent(installed, manifest)) return Fail("installed_manifest_mismatch");
            var service = serviceController.ReadPermanent();
            if (service is null || !await IsOwnedServiceAsync(service, installed, cancellationToken).ConfigureAwait(false)) return Fail("service_ownership_unproven");
            if (serviceController.GetStatus() != ServiceStatus.Running) return Fail("service_not_running");
            if (!certificatePrerequisite.IsSatisfied(manifest, out var certificateFailure)) return Fail(certificateFailure);
            if (!await healthProbe.ProbeAsync(cancellationToken).ConfigureAwait(false)) return Fail("agent_health_failed");
            stateStore.Clear();
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Fail("health_verification_unknown");
        }
    }

    /// <summary>
    /// Restores a retained PREVIOUS package after a failed operation. The failed operation's own
    /// (incoming) manifest is never consulted here -- that was the root cause of a real upgrade
    /// rollback always failing closed as a manifest mismatch. Instead the retained slot and the
    /// durable checkpoint's <see cref="AgentPackageLifecycleState.PreviousVersion"/> together define
    /// what "the previous trusted package" means, and the exact bytes activated are re-extracted from
    /// the retained signed archive and re-verified, never copied from a raw persisted payload
    /// directory. A restore is not terminal until it passes the same health gate as a forward
    /// activation; on any failure the checkpoint is preserved as recovery evidence, never cleared.
    /// </summary>
    public async Task<bool> RollbackAsync(AgentPackageOperationKind failedOperation, CancellationToken cancellationToken = default)
    {
        var restoreStagingRoot = Path.Combine(options.StagingRoot, "restore-" + Guid.NewGuid().ToString("N"));
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            // A failed explicit Rollback restores the CURRENT (pre-rollback) package from the bounded
            // recovery slot. Every other failed operation restores the retained PREVIOUS package from
            // the rollback slot. Neither slot is ever selected or matched using the failed operation's
            // incoming manifest.
            var slotRoot = failedOperation == AgentPackageOperationKind.Rollback ? options.RecoveryRoot : options.RollbackRoot;

            var checkpoint = stateStore.Read();
            if (checkpoint is null || string.IsNullOrWhiteSpace(checkpoint.PreviousVersion)) return Fail("rollback_target_unconfirmed");

            var retainedManifest = await ReadManifestAsync(Path.Combine(slotRoot, "manifest.json"), cancellationToken).ConfigureAwait(false);
            if (retainedManifest is null || !string.Equals(retainedManifest.Version, checkpoint.PreviousVersion, StringComparison.Ordinal)) return FailAndCheckpoint("rollback_manifest_mismatch");
            if (!string.Equals(retainedManifest.ReleaseChannel, options.ReleaseChannel, StringComparison.Ordinal)) return FailAndCheckpoint("package_channel_not_configured");

            var verification = failedOperation == AgentPackageOperationKind.Rollback
                ? await verifier.VerifyRecoveryAsync(retainedManifest, cancellationToken).ConfigureAwait(false)
                : await verifier.VerifyRollbackAsync(retainedManifest, cancellationToken).ConfigureAwait(false);
            if (verification.State != AgentPackageVerificationState.Verified) return FailAndCheckpoint("rollback_trust_failed");

            // Re-extract the exact verified bytes into a brand-new unique staging directory rather
            // than trusting any previously copied directory: this closes the TOCTOU gap between "the
            // signed artifact" and "the bytes actually activated."
            var archivePath = Path.Combine(slotRoot, retainedManifest.PackageId + "-" + retainedManifest.Version + ".zip");
            ServiceOwnedDirectoryProvisioner.EnsureProvisioned(restoreStagingRoot);
            await AgentPackageArchiveStaging.ExtractExactFilesAsync(archivePath, restoreStagingRoot, retainedManifest, cancellationToken).ConfigureAwait(false);
            if (!await AgentPackageArchiveStaging.VerifyStagedFilesAsync(restoreStagingRoot, retainedManifest, cancellationToken).ConfigureAwait(false)) return FailAndCheckpoint("rollback_staged_mismatch");
            // Written only after the exact staged file-set is verified, so the strict manifest/file-set
            // comparison above is never polluted by this control file, and the restored installation
            // carries the same retained, re-verified manifest the post-restore health gate checks against.
            await WriteManifestAsync(restoreStagingRoot, retainedManifest, cancellationToken).ConfigureAwait(false);

            var current = await ReadInstalledManifestAsync(cancellationToken).ConfigureAwait(false);
            var service = serviceController.ReadPermanent();
            if (service is not null
                && (current is null || !await IsOwnedServiceAsync(service, current, cancellationToken).ConfigureAwait(false))) return FailAndCheckpoint("service_ownership_conflict");
            if (!StopOwnedServiceBeforeReplacement(cancellationToken)) return FailAndCheckpoint("owned_service_stop_failed");
            if (!ReplaceInstallation(restoreStagingRoot, cancellationToken)) return FailAndCheckpoint("rollback_activation_failed");

            var executable = GetExecutablePath(retainedManifest);
            if (executable is null) return FailAndCheckpoint("rollback_executable_invalid");
            if (!certificatePrerequisite.IsSatisfied(retainedManifest, out var certificateFailure)) return FailAndCheckpoint(certificateFailure);
            if (!serviceController.EnsureConfigured(executable) || !serviceController.Start(cancellationToken)) return FailAndCheckpoint("rollback_service_activation_failed");

            // Terminal success requires the same restored-manifest, ownership, running-status,
            // certificate, and live HTTPS health truth as any forward activation -- files copied and
            // StartService succeeding are not, by themselves, a successful rollback.
            if (!await VerifyHealthAsync(retainedManifest, cancellationToken).ConfigureAwait(false)) return FailAndCheckpoint(LastFailureCode);

            if (failedOperation == AgentPackageOperationKind.Rollback) TryCleanupRetainedSlot(options.RecoveryRoot);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return FailAndCheckpoint("rollback_unknown");
        }
        finally
        {
            try { if (Directory.Exists(restoreStagingRoot) && !HasReparsePoint(restoreStagingRoot)) Directory.Delete(restoreStagingRoot, recursive: true); } catch { }
        }
    }

    private async Task<bool> UninstallAsync(CancellationToken cancellationToken)
    {
        var installed = await ReadInstalledManifestAsync(cancellationToken).ConfigureAwait(false);
        if (installed is null) return true;
        var service = serviceController.ReadPermanent();
        if (service is null || !await IsOwnedServiceAsync(service, installed, cancellationToken).ConfigureAwait(false)) return Fail("service_ownership_conflict");
        if (!await VerifyCurrentInstallAsync(installed, cancellationToken).ConfigureAwait(false)) return false;
        if (!StopOwnedServiceBeforeReplacement(cancellationToken)) return Fail("owned_service_stop_failed");
        if (!serviceController.Delete(cancellationToken)) return Fail("owned_service_delete_failed");
        if (HasReparsePoint(options.InstallationRoot)) return Fail("installation_reparse_point");
        if (Directory.Exists(options.InstallationRoot)) Directory.Delete(options.InstallationRoot, recursive: true);
        // Audit, trust, browser policy, and enterprise certificates survive local uninstall.
        stateStore.Clear();
        return true;
    }

    private async Task<bool> VerifyCurrentInstallAsync(AgentPackageManifest installed, CancellationToken cancellationToken)
    {
        var result = await verifier.VerifyInstalledAsync(installed, cancellationToken).ConfigureAwait(false);
        if (result.State != AgentPackageVerificationState.Verified)
        {
            LastFailureCode = result.Blockers.FirstOrDefault() ?? "installed_package_unverified";
            return false;
        }

        var service = serviceController.ReadPermanent();
        return service is null || await IsOwnedServiceAsync(service, installed, cancellationToken).ConfigureAwait(false);
    }

    private bool StopOwnedServiceBeforeReplacement(CancellationToken cancellationToken)
    {
        return serviceController.GetStatus() switch
        {
            ServiceStatus.Stopped or ServiceStatus.NotFound => true,
            ServiceStatus.Running or ServiceStatus.Transitioning => serviceController.Stop(cancellationToken),
            _ => false
        };
    }

    private async Task<bool> IsOwnedServiceAsync(AgentServiceEvidence evidence, AgentPackageManifest manifest, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var expectedExecutable = GetExecutablePath(manifest);
        if (expectedExecutable is null
            || evidence.HasArguments != false
            || !string.Equals(evidence.ServiceName, AgentProductIdentity.PermanentServiceName, StringComparison.Ordinal)
            || !string.Equals(evidence.DisplayName, AgentProductIdentity.ServiceDisplayName, StringComparison.Ordinal)
            || !string.Equals(evidence.Description, AgentProductIdentity.ServiceDescription, StringComparison.Ordinal)
            || !string.Equals(evidence.ServiceAccount, "LocalSystem", StringComparison.OrdinalIgnoreCase)
            || (evidence.StartType is not null && evidence.StartType is not ("2" or "Auto"))
            || !string.Equals(Path.GetFullPath(evidence.BinaryPath ?? string.Empty), Path.GetFullPath(expectedExecutable), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var binaryHash = await ComputeHashAsync(expectedExecutable, cancellationToken).ConfigureAwait(false);
        var assessment = AgentOwnershipPolicy.AssessPermanent(
            evidence with
            {
                PackageId = manifest.PackageId,
                PackageVersion = manifest.Version,
                OwnershipMarker = AgentProductIdentity.ProductId,
                BinarySha256 = binaryHash
            },
            options.InstallationRoot,
            manifest.PackageId,
            manifest.Version);
        return assessment.State == AgentResourceOwnershipState.Owned;
    }

    // Retains the PREVIOUS package for automatic rollback of a failed Install/Upgrade/Repair.
    private Task<bool> PreserveRollbackAsync(AgentPackageManifest installed, CancellationToken cancellationToken) =>
        PreserveRetainedSlotAsync(options.RollbackRoot, installed, cancellationToken);

    // Retains the CURRENT package for recovery from a failed explicit Rollback. Bounded to a single
    // fixed service-owned root; the prior contents of the slot are always discarded first, so it can
    // only ever hold the most recent one-operation recovery source.
    private Task<bool> PreserveRecoveryAsync(AgentPackageManifest installed, CancellationToken cancellationToken) =>
        PreserveRetainedSlotAsync(options.RecoveryRoot, installed, cancellationToken);

    /// <summary>
    /// Retains only the signed manifest and the already-verified available-package archive -- never a
    /// raw copy of the live installation directory. Restoration always re-extracts and re-verifies
    /// these exact bytes, so the retained slot itself carries no activation authority.
    /// </summary>
    private async Task<bool> PreserveRetainedSlotAsync(string slotRoot, AgentPackageManifest installed, CancellationToken cancellationToken)
    {
        try
        {
            if (Directory.Exists(slotRoot) && HasReparsePoint(slotRoot)) return false;
            var manifestPath = Path.Combine(slotRoot, "manifest.json");
            if (File.Exists(manifestPath)) File.Delete(manifestPath);
            var archivePath = Path.Combine(slotRoot, installed.PackageId + "-" + installed.Version + ".zip");
            if (File.Exists(archivePath)) File.Delete(archivePath);
            var availableArchive = Path.Combine(options.AvailableRoot, installed.PackageId + "-" + installed.Version + ".zip");
            if (!File.Exists(availableArchive) || HasReparsePoint(availableArchive)) return false;
            File.Copy(availableArchive, archivePath, overwrite: false);
            await WriteManifestAsync(slotRoot, installed, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Clears a retained slot's manifest and archive after it has been successfully used for
    /// recovery. Only ever called after a terminal, health-verified success -- never speculatively.
    /// </summary>
    private static void TryCleanupRetainedSlot(string slotRoot)
    {
        try
        {
            if (!Directory.Exists(slotRoot) || HasReparsePoint(slotRoot)) return;
            foreach (var file in Directory.EnumerateFiles(slotRoot)) File.Delete(file);
        }
        catch
        {
            // Best-effort cleanup only; a leftover retained slot is inert until the next preservation
            // overwrites it, and is never itself trusted as activation authority.
        }
    }

    private bool ReplaceInstallation(string stagedRoot, CancellationToken cancellationToken)
    {
        if (Directory.Exists(options.InstallationRoot))
        {
            if (HasReparsePoint(options.InstallationRoot)) return false;
            Directory.Delete(options.InstallationRoot, recursive: true);
        }

        if (!CopyDirectoryExact(stagedRoot, options.InstallationRoot, cancellationToken)) return false;
        ServiceOwnedDirectoryProvisioner.EnsureProvisioned(options.InstallationRoot);
        return true;
    }

    private async Task<bool> VerifyStagedRootAsync(string stagedRoot, AgentPackageManifest manifest, CancellationToken cancellationToken)
    {
        var expected = manifest.Files.Select(file => file.RelativePath.Replace('\\', '/')).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var actual = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in Directory.EnumerateFileSystemEntries(stagedRoot, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (HasReparsePoint(entry)) return false;
            if (Directory.Exists(entry)) continue;
            var relative = Path.GetRelativePath(stagedRoot, entry).Replace('\\', '/');
            if (!expected.Contains(relative) || !actual.Add(relative)) return false;
        }

        if (!expected.SetEquals(actual)) return false;
        foreach (var file in manifest.Files)
        {
            var path = Path.GetFullPath(Path.Combine(stagedRoot, file.RelativePath));
            if (!path.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(stagedRoot)) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || !File.Exists(path)
                || new FileInfo(path).Length != file.SizeBytes
                || !string.Equals(await ComputeHashAsync(path, cancellationToken).ConfigureAwait(false), file.Sha256, StringComparison.OrdinalIgnoreCase)) return false;
        }

        return true;
    }

    private async Task<AgentPackageManifest?> ReadInstalledManifestAsync(CancellationToken cancellationToken) =>
        await ReadManifestAsync(Path.Combine(options.InstallationRoot, "manifest.json"), cancellationToken).ConfigureAwait(false);

    private static async Task<AgentPackageManifest?> ReadManifestAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(path)
                || File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint)
                || new FileInfo(path).Length is <= 0 or > 1024 * 1024) return null;
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return await JsonSerializer.DeserializeAsync<AgentPackageManifest>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private async Task WriteManifestAsync(string root, AgentPackageManifest manifest, CancellationToken cancellationToken)
    {
        ServiceOwnedDirectoryProvisioner.EnsureProvisioned(root);
        var path = Path.Combine(root, "manifest.json");
        var temporary = path + ".tmp";
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(manifest, JsonOptions), cancellationToken).ConfigureAwait(false);
        if (File.GetAttributes(temporary).HasFlag(FileAttributes.ReparsePoint)) throw new IOException("The manifest became a reparse point.");
        File.Move(temporary, path, overwrite: true);
    }

    private static bool CopyDirectoryExact(string sourceRoot, string destinationRoot, CancellationToken cancellationToken)
    {
        try
        {
            if (HasReparsePoint(sourceRoot)) return false;
            Directory.CreateDirectory(destinationRoot);
            foreach (var directory in Directory.EnumerateDirectories(sourceRoot, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (HasReparsePoint(directory)) return false;
                Directory.CreateDirectory(Path.Combine(destinationRoot, Path.GetRelativePath(sourceRoot, directory)));
            }

            foreach (var file in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (HasReparsePoint(file)) return false;
                var target = Path.Combine(destinationRoot, Path.GetRelativePath(sourceRoot, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target, overwrite: false);
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    private string? GetExecutablePath(AgentPackageManifest manifest)
    {
        var executable = manifest.Files
            .Where(file => file.RelativePath.IndexOfAny(['/', '\\']) < 0)
            .Where(file => AgentProductIdentity.AllowedExecutableNames.Contains(file.RelativePath))
            .ToArray();
        return executable.Length == 1 ? Path.Combine(options.InstallationRoot, executable[0].RelativePath) : null;
    }

    private static bool ValidateTransition(AgentPackageOperationKind operation, string current, string incoming, out string failure)
    {
        failure = "none";
        if (!AgentPackageVersion.TryParse(current, out _) || !AgentPackageVersion.TryParse(incoming, out _))
        {
            failure = "package_version_invalid";
            return false;
        }

        var comparison = AgentPackageVersion.Compare(incoming, current);
        if (operation is AgentPackageOperationKind.Install or AgentPackageOperationKind.Upgrade && comparison < 0) { failure = "unsupported_downgrade"; return false; }
        if (operation == AgentPackageOperationKind.Rollback && comparison >= 0) { failure = "rollback_version_invalid"; return false; }
        if (operation == AgentPackageOperationKind.Repair && comparison != 0) { failure = "repair_version_mismatch"; return false; }
        return true;
    }

    private AgentPackageLifecycleState ReadState(
        string operationId,
        AgentPackageOperationKind operation,
        AgentPackageLifecyclePhase phase,
        AgentPackageManifest manifest,
        AgentPackageManifest? installed,
        string? failureCode = null,
        bool recoveryRequired = false) =>
        new(operationId, operation, phase, manifest.PackageId, manifest.Version, installed?.Version, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, failureCode, recoveryRequired);

    private bool Fail(string failureCode)
    {
        LastFailureCode = failureCode;
        return false;
    }

    private bool FailAndCheckpoint(string failureCode)
    {
        LastFailureCode = failureCode;
        try
        {
            var state = stateStore.Read();
            if (state is not null)
            {
                stateStore.Write(state with
                {
                    Phase = AgentPackageLifecyclePhase.RecoveryRequired,
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                    FailureCode = failureCode,
                    RecoveryRequired = true
                });
            }
        }
        catch
        {
            LastFailureCode = "recovery_state_unavailable";
        }

        return false;
    }

    private static bool ManifestEquivalent(AgentPackageManifest left, AgentPackageManifest right) =>
        string.Equals(left.Signature, right.Signature, StringComparison.Ordinal)
        && AgentPackageCanonicalizer.Canonicalize(left).AsSpan().SequenceEqual(AgentPackageCanonicalizer.Canonicalize(right));

    private bool IsSafeStagingRoot(string path)
    {
        try
        {
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
            var parent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(options.StagingRoot));
            return root.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                && !root.Contains("..", StringComparison.Ordinal)
                && !HasReparsePoint(root);
        }
        catch
        {
            return false;
        }
    }

    private static bool HasReparsePoint(string path)
    {
        try { return File.Exists(path) || Directory.Exists(path) ? File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint) : true; }
        catch { return true; }
    }

    private static async Task<string> ComputeHashAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false)).ToLowerInvariant();
    }
}

/// <summary>
/// Versioned package boundary. It extracts only exact signed files, re-verifies the staged tree,
/// and delegates activation/health/rollback truth to the typed platform.
/// </summary>
public sealed class FileAgentPackageLifecycle(
    AgentPackageOptions options,
    FileAgentPackageVerifier verifier,
    IAgentPackageInstallationPlatform platform) : IAgentPackageLifecycle
{
    public async Task<AgentPackageExecutionResult> ExecuteAsync(AgentPackageExecutionRequest request, CancellationToken cancellationToken = default)
    {
        options.EnsureStorageProvisioned();
        var validation = request.Operation switch
        {
            AgentPackageOperationKind.Uninstall => await verifier.VerifyInstalledAsync(request.Manifest, cancellationToken).ConfigureAwait(false),
            AgentPackageOperationKind.Rollback => await verifier.VerifyRollbackAsync(request.Manifest, cancellationToken).ConfigureAwait(false),
            _ => await verifier.VerifyAsync(request.Manifest, cancellationToken).ConfigureAwait(false)
        };
        if (validation.State != AgentPackageVerificationState.Verified) return new(AgentPackageOperationState.Failed, false, false, false, validation.Detail);

        if (request.Operation == AgentPackageOperationKind.Health)
        {
            var healthy = await platform.VerifyHealthAsync(request.Manifest, cancellationToken).ConfigureAwait(false);
            return healthy
                ? new(AgentPackageOperationState.Completed, false, false, false, "The Agent health endpoints and owned service state are healthy.")
                : new(AgentPackageOperationState.Failed, false, false, false, "The Agent health prerequisite could not be verified.");
        }

        var stagingRoot = Path.Combine(options.StagingRoot, request.Operation.ToString().ToLowerInvariant() + "-" + Guid.NewGuid().ToString("N"));
        var activationAttempted = false;
        try
        {
            if (!await platform.PrepareAsync(request.Manifest, request.Operation, cancellationToken).ConfigureAwait(false)) return new(AgentPackageOperationState.RecoveryRequired, false, false, true, "The Agent platform precondition failed; package activation was not attempted.");
            ServiceOwnedDirectoryProvisioner.EnsureProvisioned(stagingRoot);
            if (request.Operation != AgentPackageOperationKind.Uninstall)
            {
                var archive = request.Operation == AgentPackageOperationKind.Rollback ? verifier.RollbackArchivePath(request.Manifest) : verifier.ArchivePath(request.Manifest);
                await ExtractExactFilesAsync(archive, stagingRoot, request.Manifest, cancellationToken).ConfigureAwait(false);
                if (!await VerifyStagedFilesAsync(stagingRoot, request.Manifest, cancellationToken).ConfigureAwait(false)) throw new InvalidDataException("The staged package changed after extraction.");
            }

            activationAttempted = true;
            if (!await platform.ActivateAsync(stagingRoot, request.Manifest, request.Operation, cancellationToken).ConfigureAwait(false))
            {
                var rollback = await SafeRollbackAsync(request.Operation, CancellationToken.None).ConfigureAwait(false);
                return rollback ? new(AgentPackageOperationState.RollbackSucceeded, true, true, false, "Activation failed; the prior Agent package was restored and verified by the platform.") : new(AgentPackageOperationState.RollbackFailed, true, false, true, "Activation failed and rollback could not be confirmed; recovery is required.");
            }

            if (request.Operation == AgentPackageOperationKind.Uninstall)
            {
                return new(AgentPackageOperationState.Completed, false, false, false, "The owned Agent service and installation were removed; durable audit and external certificate state were retained.");
            }

            if (!await platform.VerifyHealthAsync(request.Manifest, cancellationToken).ConfigureAwait(false))
            {
                var rollback = await SafeRollbackAsync(request.Operation, CancellationToken.None).ConfigureAwait(false);
                return rollback ? new(AgentPackageOperationState.RollbackSucceeded, true, true, false, "Post-activation health failed; the prior Agent package was restored and verified.") : new(AgentPackageOperationState.RollbackFailed, true, false, true, "Post-activation health failed and rollback could not be confirmed; recovery is required.");
            }

            return new(AgentPackageOperationState.Completed, false, false, false, "The verified Agent package was activated and health was confirmed.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (!activationAttempted) return new(AgentPackageOperationState.Failed, false, false, false, "Package staging was cancelled before activation began.");
            var rollback = await SafeRollbackAsync(request.Operation, CancellationToken.None).ConfigureAwait(false);
            return rollback ? new(AgentPackageOperationState.RollbackSucceeded, true, true, false, "Package activation was cancelled; the prior package was restored.") : new(AgentPackageOperationState.RecoveryRequired, true, false, true, "Package activation was cancelled and recovery could not be confirmed.");
        }
        catch
        {
            if (!activationAttempted) return new(AgentPackageOperationState.Failed, false, false, false, "Package staging failed before activation began.");
            var rollback = await SafeRollbackAsync(request.Operation, CancellationToken.None).ConfigureAwait(false);
            return rollback ? new(AgentPackageOperationState.RollbackSucceeded, true, true, false, "Package activation failed; the prior package was restored.") : new(AgentPackageOperationState.RollbackFailed, true, false, true, "Package activation failed and rollback could not be confirmed; recovery is required.");
        }
        finally
        {
            try { if (Directory.Exists(stagingRoot) && !HasReparsePoint(stagingRoot)) Directory.Delete(stagingRoot, recursive: true); } catch { }
        }
    }

    private async Task<bool> SafeRollbackAsync(AgentPackageOperationKind failedOperation, CancellationToken cancellationToken)
    {
        try { return await platform.RollbackAsync(failedOperation, cancellationToken).ConfigureAwait(false); } catch { return false; }
    }

    private static Task ExtractExactFilesAsync(string archivePath, string stagingRoot, AgentPackageManifest manifest, CancellationToken cancellationToken) =>
        AgentPackageArchiveStaging.ExtractExactFilesAsync(archivePath, stagingRoot, manifest, cancellationToken);

    private static Task<bool> VerifyStagedFilesAsync(string root, AgentPackageManifest manifest, CancellationToken cancellationToken) =>
        AgentPackageArchiveStaging.VerifyStagedFilesAsync(root, manifest, cancellationToken);

    private static bool HasReparsePoint(string path) => AgentPackageArchiveStaging.HasReparsePoint(path);
}

/// <summary>
/// Shared exact-archive-extraction and staged-file verification, used both for forward package
/// activation and for re-extracting a retained rollback/recovery archive. Centralizing this keeps the
/// same reparse/traversal/exact-file-set/hash rules in force everywhere bytes are staged for
/// activation, instead of trusting a previously copied directory.
/// </summary>
internal static class AgentPackageArchiveStaging
{
    public static async Task ExtractExactFilesAsync(string archivePath, string stagingRoot, AgentPackageManifest manifest, CancellationToken cancellationToken)
    {
        using var archive = System.IO.Compression.ZipFile.OpenRead(archivePath);
        var expected = manifest.Files.ToDictionary(file => file.RelativePath.Replace('\\', '/'), StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (archive.Entries.Count != expected.Count) throw new InvalidDataException("The package archive contains an unexpected file set.");
        foreach (var entry in archive.Entries)
        {
            var normalized = entry.FullName.Replace('\\', '/');
            if (IsArchiveLink(entry) || normalized.StartsWith("/", StringComparison.Ordinal) || normalized.Split('/').Any(segment => segment is "" or "." or "..") || !seen.Add(normalized) || !expected.TryGetValue(normalized, out var file)) throw new InvalidDataException("The package archive contains an unsafe or unexpected path.");
            var target = Path.GetFullPath(Path.Combine(stagingRoot, normalized));
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(stagingRoot)) + Path.DirectorySeparatorChar;
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The package archive escaped the staging root.");
            var targetDirectory = Path.GetDirectoryName(target)!;
            Directory.CreateDirectory(targetDirectory);
            if (HasReparsePoint(targetDirectory)) throw new InvalidDataException("The package staging path contains a reparse point.");
            using (var source = entry.Open())
            using (var destination = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await CopyExactAsync(source, destination, file.SizeBytes, cancellationToken).ConfigureAwait(false);
            }
            var stagedHash = await ComputeHashAsync(target, cancellationToken).ConfigureAwait(false);
            if (HasReparsePoint(target) || new FileInfo(target).Length != file.SizeBytes || !string.Equals(stagedHash, file.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The staged package file did not match its manifest.");
        }
    }

    public static async Task<bool> VerifyStagedFilesAsync(string root, AgentPackageManifest manifest, CancellationToken cancellationToken)
    {
        var expected = manifest.Files.Select(file => file.RelativePath.Replace('\\', '/')).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var actual = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Select(path => Path.GetRelativePath(root, path).Replace('\\', '/')).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!expected.SetEquals(actual)) return false;
        foreach (var file in manifest.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = Path.Combine(root, file.RelativePath);
            if (new FileInfo(path).Length != file.SizeBytes || !string.Equals(await ComputeHashAsync(path, cancellationToken).ConfigureAwait(false), file.Sha256, StringComparison.OrdinalIgnoreCase)) return false;
        }
        return true;
    }

    private static async Task CopyExactAsync(Stream source, Stream destination, long declaredSize, CancellationToken cancellationToken)
    {
        var buffer = new byte[64 * 1024];
        long copied = 0;
        while (true)
        {
            var read = await source.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            if (copied > declaredSize - read) throw new InvalidDataException("The package archive streamed beyond its declared file size.");
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            copied += read;
        }
        if (copied != declaredSize) throw new InvalidDataException("The package archive ended before its declared file size.");
    }

    private static async Task<string> ComputeHashAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false)).ToLowerInvariant();
    }

    private static bool IsArchiveLink(System.IO.Compression.ZipArchiveEntry entry) => ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000;

    public static bool HasReparsePoint(string path)
    {
        try { return File.Exists(path) || Directory.Exists(path) ? File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint) : true; }
        catch { return true; }
    }
}
