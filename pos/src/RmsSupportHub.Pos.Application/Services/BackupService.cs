using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using RmsSupportHub.Pos.Domain.Enums;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Application.Services;

/// <summary>
/// Application policy for the local backup workflow. All host I/O is delegated to
/// <see cref="IBackupFileSystem"/>; the service never launches a shell or Explorer.
/// </summary>
public sealed partial class BackupService(
    IDatabaseService databaseService,
    IBackupFileSystem fileSystem,
    TimeProvider? timeProvider = null)
{
    private const long MinimumFreeSpaceBytes = 1 * 1024 * 1024;
    private const long DatabaseReserveBytes = 8 * 1024 * 1024;
    private const int CopyBufferSize = 128 * 1024;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public static IReadOnlyList<BackupComponentDefinition> ComponentDefinitions { get; } =
    [
        new("branch-database", "RmsBranchSrv database", BackupComponentKind.BranchDatabase),
        new("cashier-database", "RmsCashierSrv database", BackupComponentKind.CashierDatabase),
        new("branch-config", "Branch appsettings", BackupComponentKind.BranchConfig),
        new("cashier-server-config", "Cashier server appsettings", BackupComponentKind.CashierServerConfig),
        new("cashier-ui-config", "Cashier UI appsettings", BackupComponentKind.CashierUiConfig),
    ];

    private static readonly IReadOnlyDictionary<string, BackupComponentDefinition> ComponentsById =
        ComponentDefinitions.ToDictionary(component => component.Id, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Validates the server-owned selection and destination before an operation is queued. This is
    /// intentionally free of SQL calls: validation must fail closed without touching a database.
    /// </summary>
    public BackupValidationResult Validate(
        AppSettings settings,
        IReadOnlyCollection<string> componentIds,
        string destinationPath)
    {
        var errors = new List<BackupValidationError>();
        var selected = NormalizeComponentIds(componentIds, errors);

        BackupDestinationInfo destination;
        try
        {
            destination = string.IsNullOrWhiteSpace(destinationPath)
                ? new BackupDestinationInfo(false, false, 0)
                : fileSystem.InspectDestination(destinationPath);
        }
        catch
        {
            destination = new BackupDestinationInfo(false, false, 0);
        }

        if (!destination.Exists || !destination.IsDirectory)
        {
            errors.Add(new(BackupValidationErrorCodes.DestinationInvalid, "The selected backup destination is unavailable."));
        }

        var branchCode = settings.BranchCode?.Trim() ?? string.Empty;
        if (branchCode.Length == 0 || branchCode.Length > 50)
        {
            errors.Add(new(BackupValidationErrorCodes.BranchInvalid, "A configured branch identity is required."));
        }

        var selectedDatabases = selected
            .Where(component => component.Kind is BackupComponentKind.BranchDatabase or BackupComponentKind.CashierDatabase)
            .Select(component => (component, database: ResolveDatabaseName(settings, component.Kind)))
            .ToList();

        foreach (var (_, database) in selectedDatabases)
        {
            if (string.IsNullOrWhiteSpace(database) || !SafeDatabaseIdentifierRegex().IsMatch(database))
            {
                errors.Add(new(BackupValidationErrorCodes.DatabaseInvalid, "A selected database identifier is not valid."));
            }
        }

        long estimatedRequired = MinimumFreeSpaceBytes + (selectedDatabases.Count * DatabaseReserveBytes);
        foreach (var component in selected.Where(component => component.Kind is BackupComponentKind.BranchConfig or BackupComponentKind.CashierServerConfig or BackupComponentKind.CashierUiConfig))
        {
            var sourcePath = ResolveConfigPath(settings, component.Kind);
            if (string.IsNullOrWhiteSpace(sourcePath)
                || !fileSystem.FileExists(sourcePath)
                || fileSystem.IsReparsePoint(sourcePath))
            {
                errors.Add(new(BackupValidationErrorCodes.ConfigurationSourceMissing, $"The selected {component.DisplayName} source is unavailable."));
                continue;
            }

            try
            {
                estimatedRequired += Math.Max(0, fileSystem.GetFileLength(sourcePath));
            }
            catch
            {
                errors.Add(new(BackupValidationErrorCodes.ConfigurationSourceMissing, $"The selected {component.DisplayName} source is unavailable."));
            }
        }

        if (destination.AvailableFreeSpaceBytes < estimatedRequired)
        {
            errors.Add(new(BackupValidationErrorCodes.InsufficientSpace, "The selected destination does not have enough free space for this backup."));
        }

        return new(
            errors.Count == 0,
            destination.AvailableFreeSpaceBytes,
            estimatedRequired,
            errors,
            branchCode,
            selectedDatabases.FirstOrDefault(database => database.component.Kind == BackupComponentKind.BranchDatabase).database
                ?? ResolveDatabaseName(settings, BackupComponentKind.BranchDatabase));
    }

    /// <summary>Executes a validated Agent backup and returns an internal artifact descriptor.</summary>
    public async Task<BackupExecutionResult> ExecuteAsync(
        AppSettings settings,
        IReadOnlyCollection<string> componentIds,
        string destinationPath,
        IProgress<BackupProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var operation = OperationResult.Running("backup_database");
        var validation = Validate(settings, componentIds, destinationPath);
        if (!validation.Ready)
        {
            foreach (var error in validation.Errors) operation.AddError(error.Message);
            operation.Finalize(OperationStatus.Failed);
            return new(operation, null);
        }

        var selected = NormalizeComponentIds(componentIds, []);
        var createdAtUtc = _timeProvider.GetUtcNow();
        var timestamp = createdAtUtc.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var archiveName = string.Concat(
            SafeFilenamePart(settings.ClientName, "Client"),
            "_",
            SafeFilenamePart(settings.BranchCode, "Branch"),
            "_POS_",
            SafeFilenamePart(settings.PosNumber, "00"),
            "_DB_Backup_",
            timestamp,
            ".zip");

        var destination = Path.GetFullPath(destinationPath);
        var stagingDirectory = Path.Combine(destination, $".pos_backup_{Guid.NewGuid():N}");
        var temporaryArchive = Path.Combine(destination, $".{archiveName}.{Guid.NewGuid():N}.tmp");
        var archivePath = Path.Combine(destination, archiveName);
        var staged = new List<StagedBackupFile>();
        var errors = new List<string>();
        BackupArtifactResult? artifact = null;
        var archiveCreatedByThisExecution = false;

        try
        {
            await fileSystem.EnsureDirectoryAsync(stagingDirectory, cancellationToken).ConfigureAwait(false);
            Report(progress, 5, "validated", "Backup preflight completed.");

            foreach (var component in selected)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (component.Kind is BackupComponentKind.BranchDatabase or BackupComponentKind.CashierDatabase)
                {
                    var databaseName = ResolveDatabaseName(settings, component.Kind);
                    var archiveFileName = $"{SafeFilenamePart(databaseName, "database")}_{timestamp}.bak";
                    var stagedPath = Path.Combine(stagingDirectory, archiveFileName);
                    Report(progress, 10 + (staged.Count * 10), "database", $"Creating SQL backup for {component.DisplayName}...");

                    try
                    {
                        await databaseService.BackupDatabaseAsync(settings, databaseName, stagedPath, useCompatibilityMode: false, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch
                    {
                        Report(progress, 10 + (staged.Count * 10), "database-retry", $"Retrying SQL backup for {component.DisplayName} with compatibility mode.");
                        try
                        {
                            await databaseService.BackupDatabaseAsync(settings, databaseName, stagedPath, useCompatibilityMode: true, cancellationToken).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch
                        {
                            errors.Add($"The {component.DisplayName} backup failed.");
                            continue;
                        }
                    }

                    if (!fileSystem.FileExists(stagedPath))
                    {
                        errors.Add($"The {component.DisplayName} backup was not created.");
                        continue;
                    }

                    staged.Add(await DescribeStagedFileAsync(component, archiveFileName, stagedPath, cancellationToken).ConfigureAwait(false));
                    continue;
                }

                var sourcePath = ResolveConfigPath(settings, component.Kind);
                var archiveFile = SafeArchiveName(component.Id switch
                {
                    "branch-config" => "RMS_BranchService_appsettings.json",
                    "cashier-server-config" => "RMS_CashierServer_appsettings.json",
                    _ => "RMS_CashierUI_appsettings.json",
                }, "appsettings.json");
                var stagedConfigPath = Path.Combine(stagingDirectory, archiveFile);
                Report(progress, 10 + (staged.Count * 10), "configuration", $"Copying {component.DisplayName}...");

                try
                {
                    await fileSystem.CopyFileAsync(sourcePath, stagedConfigPath, cancellationToken).ConfigureAwait(false);
                    staged.Add(await DescribeStagedFileAsync(component, archiveFile, stagedConfigPath, cancellationToken).ConfigureAwait(false));
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    errors.Add($"The {component.DisplayName} source could not be copied.");
                }
            }

            if (staged.Count == 0)
            {
                operation.AddError("No selected backup item could be staged.");
                foreach (var error in errors) operation.AddError(error);
                operation.Finalize(OperationStatus.Failed);
                return new(operation, null);
            }

            Report(progress, 75, "compressing", "Compressing selected backup items.");
            var manifest = new BackupManifest(
                1,
                settings.BranchCode.Trim(),
                settings.PosNumber?.Trim() ?? string.Empty,
                settings.Release?.Trim() ?? string.Empty,
                createdAtUtc,
                staged.Select(item => new BackupManifestItem(item.Component.Id, item.Component.DisplayName, item.ArchiveName, item.SizeBytes, item.Sha256Checksum)).ToList(),
                errors);
            var manifestJson = JsonSerializer.Serialize(manifest, ManifestJsonOptions);

            await using (var archiveStream = await fileSystem.CreateFileAsync(temporaryArchive, cancellationToken).ConfigureAwait(false))
            using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, leaveOpen: false))
            {
                foreach (var item in staged)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var entry = archive.CreateEntry(item.ArchiveName, CompressionLevel.Optimal);
                    await using var source = await fileSystem.OpenReadAsync(item.StagedPath, cancellationToken).ConfigureAwait(false);
                    await using var target = entry.Open();
                    await source.CopyToAsync(target, CopyBufferSize, cancellationToken).ConfigureAwait(false);
                }

                var manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
                await using var manifestStream = manifestEntry.Open();
                var manifestBytes = Encoding.UTF8.GetBytes(manifestJson);
                await manifestStream.WriteAsync(manifestBytes, cancellationToken).ConfigureAwait(false);
            }

            await fileSystem.MoveFileAsync(temporaryArchive, archivePath, overwrite: false, cancellationToken).ConfigureAwait(false);
            archiveCreatedByThisExecution = true;
            var archiveSize = fileSystem.GetFileLength(archivePath);
            var archiveChecksum = await fileSystem.ComputeSha256Async(archivePath, cancellationToken).ConfigureAwait(false);
            artifact = new BackupArtifactResult(archivePath, archiveName, archiveSize, archiveChecksum, createdAtUtc);

            foreach (var error in errors) operation.AddError(error);
            operation.AddMessage(errors.Count == 0 ? "Backup archive created." : "Backup archive created with partial component failures.");
            operation.Context["zip_file"] = archivePath;
            operation.Finalize(errors.Count == 0 ? OperationStatus.Success : OperationStatus.PartialSuccess);
            Report(progress, 100, errors.Count == 0 ? "completed" : "partial", errors.Count == 0 ? "Backup completed." : "Backup completed with partial failures.");
            return new(operation, artifact);
        }
        catch (OperationCanceledException)
        {
            operation.AddMessage("Backup cancelled.");
            operation.Finalize(OperationStatus.Cancelled);
            return new(operation, null);
        }
        catch
        {
            operation.AddError("Backup failed while creating the archive.");
            operation.Finalize(OperationStatus.Failed);
            return new(operation, null);
        }
        finally
        {
            try { await fileSystem.DeleteFileAsync(temporaryArchive).ConfigureAwait(false); } catch { }
            if (archiveCreatedByThisExecution && artifact is null)
            {
                try { await fileSystem.DeleteFileAsync(archivePath).ConfigureAwait(false); } catch { }
            }

            try { await fileSystem.DeleteDirectoryAsync(stagingDirectory).ConfigureAwait(false); } catch { }
        }
    }

    /// <summary>
    /// Compatibility entry point used by the retained WinUI application. The Agent uses
    /// <see cref="ExecuteAsync"/> with a redeemed destination handle.
    /// </summary>
    public async Task<OperationResult> BackupAsync(
        AppSettings settings,
        IReadOnlyCollection<string> selectedItems,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var componentIds = selectedItems
            .Select(MapLegacyLabel)
            .Where(id => id is not null)
            .Select(id => id!)
            .ToList();
        var progressAdapter = progress is null
            ? null
            : new Progress<BackupProgress>(update => progress.Report(update.Message));
        var result = await ExecuteAsync(settings, componentIds, settings.BackupFolder, progressAdapter, cancellationToken).ConfigureAwait(false);
        if (result.Artifact is not null)
        {
            result.Operation.Context["zip_file"] = result.Artifact.ArchivePath;
        }

        return result.Operation;
    }

    private async Task<StagedBackupFile> DescribeStagedFileAsync(
        BackupComponentDefinition component,
        string archiveName,
        string stagedPath,
        CancellationToken cancellationToken)
    {
        return new(
            component,
            archiveName,
            stagedPath,
            fileSystem.GetFileLength(stagedPath),
            await fileSystem.ComputeSha256Async(stagedPath, cancellationToken).ConfigureAwait(false));
    }

    private static List<BackupComponentDefinition> NormalizeComponentIds(
        IEnumerable<string> componentIds,
        List<BackupValidationError> errors)
    {
        var selected = new List<BackupComponentDefinition>();
        foreach (var rawId in componentIds ?? [])
        {
            var id = rawId?.Trim() ?? string.Empty;
            if (id.Length == 0) continue;
            if (!ComponentsById.TryGetValue(id, out var component))
            {
                // Retain the old WinUI labels at the application boundary only. Browser requests
                // use IDs and are rejected if they do not map to a known component.
                var legacyId = MapLegacyLabel(id);
                if (legacyId is null || !ComponentsById.TryGetValue(legacyId, out component))
                {
                    errors.Add(new(BackupValidationErrorCodes.UnknownComponent, "A selected backup component is not supported."));
                    continue;
                }
            }

            if (selected.All(existing => !string.Equals(existing.Id, component.Id, StringComparison.OrdinalIgnoreCase)))
            {
                selected.Add(component);
            }
        }

        if (selected.Count == 0)
        {
            errors.Add(new(BackupValidationErrorCodes.NoComponents, "Select at least one backup component."));
        }

        return selected;
    }

    private static string? MapLegacyLabel(string label)
    {
        var normalized = NormalizeLabel(label);
        return normalized switch
        {
            "rmsbranchsrv database" or "branch database" or "branch-database" => "branch-database",
            "rmscashiersrv database" or "cashier database" or "cashier-database" => "cashier-database",
            "rms_branchservice_appsettings.json" or "branch-config" => "branch-config",
            "rms_cashierserver_appsettings.json" or "cashier-server-config" => "cashier-server-config",
            "rms_cashierui_appsettings.json" or "cashier-ui-config" => "cashier-ui-config",
            _ => null,
        };
    }

    private static string ResolveDatabaseName(AppSettings settings, BackupComponentKind kind)
    {
        var usesBranch = kind == BackupComponentKind.BranchDatabase;
        var configured = settings.Databases.FirstOrDefault(database =>
            database.Contains(usesBranch ? "branch" : "cashier", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(configured)) return configured.Trim();
        return usesBranch ? "RmsBranchSrv" : "RmsCashierSrv";
    }

    private static string ResolveConfigPath(AppSettings settings, BackupComponentKind kind) => kind switch
    {
        BackupComponentKind.BranchConfig => settings.BranchConfigPath,
        BackupComponentKind.CashierServerConfig => settings.CashierGrpcConfigPath,
        BackupComponentKind.CashierUiConfig => settings.CashierUiConfigPath,
        _ => string.Empty,
    };

    private static string NormalizeLabel(string text) =>
        string.Join(' ', (text ?? string.Empty).Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static void Report(IProgress<BackupProgress>? progress, int percent, string stage, string message) =>
        progress?.Report(new BackupProgress(Math.Clamp(percent, 0, 100), stage, message));

    private static string SafeFilenamePart(string? text, string fallback)
    {
        var candidate = string.IsNullOrWhiteSpace(text) ? fallback : text.Trim();
        candidate = UnsafeFilenamePartRegex().Replace(candidate, "_");
        candidate = RepeatedUnderscoreRegex().Replace(candidate, "_").Trim('_');
        return string.IsNullOrWhiteSpace(candidate) ? fallback : candidate;
    }

    private static string SafeArchiveName(string? text, string fallback)
    {
        var candidate = string.IsNullOrWhiteSpace(text) ? fallback : text.Trim();
        candidate = UnsafeArchiveNameRegex().Replace(candidate, "_").Replace("..", "_", StringComparison.Ordinal);
        return string.IsNullOrWhiteSpace(candidate) ? fallback : candidate;
    }

    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [GeneratedRegex("^[A-Za-z0-9_.-]+$")]
    private static partial Regex SafeDatabaseIdentifierRegex();

    [GeneratedRegex("[^A-Za-z0-9_-]+")]
    private static partial Regex UnsafeFilenamePartRegex();

    [GeneratedRegex("_+")]
    private static partial Regex RepeatedUnderscoreRegex();

    [GeneratedRegex("[<>:\"/\\\\|?*]+")]
    private static partial Regex UnsafeArchiveNameRegex();

    private sealed record StagedBackupFile(
        BackupComponentDefinition Component,
        string ArchiveName,
        string StagedPath,
        long SizeBytes,
        string Sha256Checksum);

    private sealed record BackupManifest(
        int SchemaVersion,
        string BranchCode,
        string PosNumber,
        string Release,
        DateTimeOffset CreatedAtUtc,
        IReadOnlyList<BackupManifestItem> Contents,
        IReadOnlyList<string> Warnings);

    private sealed record BackupManifestItem(
        string ComponentId,
        string DisplayName,
        string ArchiveName,
        long SizeBytes,
        string Sha256Checksum);
}

public enum BackupComponentKind
{
    BranchDatabase,
    CashierDatabase,
    BranchConfig,
    CashierServerConfig,
    CashierUiConfig,
}

public sealed record BackupComponentDefinition(string Id, string DisplayName, BackupComponentKind Kind);

public sealed record BackupProgress(int Percent, string Stage, string Message);

public sealed record BackupValidationError(string Code, string Message);

public sealed record BackupValidationResult(
    bool Ready,
    long AvailableFreeSpaceBytes,
    long EstimatedRequiredFreeSpaceBytes,
    IReadOnlyList<BackupValidationError> Errors,
    string BranchCode,
    string TargetDatabase);

public sealed record BackupArtifactResult(
    string ArchivePath,
    string DisplayName,
    long SizeBytes,
    string Sha256Checksum,
    DateTimeOffset CreatedAtUtc);

public sealed record BackupExecutionResult(OperationResult Operation, BackupArtifactResult? Artifact);

public static class BackupValidationErrorCodes
{
    public const string DestinationInvalid = "backup.destination_invalid";
    public const string NoComponents = "backup.no_components";
    public const string UnknownComponent = "backup.unknown_component";
    public const string BranchInvalid = "backup.branch_invalid";
    public const string DatabaseInvalid = "backup.database_invalid";
    public const string ConfigurationSourceMissing = "backup.configuration_source_missing";
    public const string InsufficientSpace = "backup.insufficient_space";
}
