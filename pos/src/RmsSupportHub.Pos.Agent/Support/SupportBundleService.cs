using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using RmsSupportHub.Pos.Agent.Artifacts;
using RmsSupportHub.Pos.Agent.Diagnostics;
using RmsSupportHub.Pos.Agent.Rms;
using RmsSupportHub.Pos.Contracts.V1.Common;
using RmsSupportHub.Pos.Contracts.V1.Diagnostics;
using RmsSupportHub.Pos.Contracts.V1.Support;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Agent.Support;

/// <summary>
/// Creates one bounded archive containing only typed, redacted local evidence. The archive is
/// exposed later through the existing principal-scoped opaque artifact capability.
/// </summary>
public sealed class SupportBundleService(
    RmsDiagnosticsService diagnostics,
    RmsOperationalHealthService operationalHealth,
    PosHealthService health,
    ServiceFailureAnalyzer failureAnalyzer,
    IncidentTimelineService timeline,
    ArtifactCatalog artifacts,
    IBackupFileSystem fileSystem,
    IAgentAuditReader auditReader,
    SupportBundleOptions options,
    TimeProvider timeProvider)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private static readonly string[] Sections =
    ["manifest", "health", "installation", "connectivity", "database", "services", "failure-analysis", "incident-timeline", "rms-storage", "updates", "insurance-attachment-aggregate", "audit-summary"];

    public async Task<SupportBundleDto> GenerateAsync(
        string principalSid,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        options.Validate();
        var createdAtUtc = timeProvider.GetUtcNow();
        var diagnostic = await diagnostics.GetAsync(cancellationToken).ConfigureAwait(false);
        var operational = await operationalHealth.GetAsync(cancellationToken).ConfigureAwait(false);
        var healthReport = await health.GetAsync(cancellationToken).ConfigureAwait(false);
        var audit = (await auditReader.ReadRecentAsync(64, cancellationToken).ConfigureAwait(false))
            .Select(ToSafeAudit)
            .ToArray();
        timeline.Record(
            principalSid,
            "HealthCheck",
            ToTimelineSeverity(healthReport.OverallState),
            healthReport.Summary,
            operationId: "health.check",
            correlationId: correlationId);
        var serviceAnalyses = new List<ServiceFailureAnalysisDto>();
        foreach (var service in diagnostic.Services.Take(3))
        {
            var analysis = await failureAnalyzer.AnalyzeAsync(service.ServiceId, cancellationToken).ConfigureAwait(false);
            if (analysis is not null) serviceAnalyses.Add(analysis);
        }

        var incidentTimeline = timeline.Get(principalSid);
        var payload = new SupportBundlePayload(
            "RMS+ Support Hub Support Bundle v2",
            createdAtUtc,
            correlationId,
            Sections,
            healthReport,
            diagnostic,
            operational,
            serviceAnalyses,
            incidentTimeline,
            audit);

        await fileSystem.EnsureDirectoryAsync(options.BundleRootPath, cancellationToken).ConfigureAwait(false);
        var fileName = $"rms-support-bundle-{createdAtUtc:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.zip";
        var path = Path.Combine(options.BundleRootPath, fileName);
        try
        {
            await using (var stream = await fileSystem.CreateFileAsync(path, cancellationToken).ConfigureAwait(false))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false))
            {
                var entry = archive.CreateEntry("support-bundle.json", CompressionLevel.Optimal);
                await using var entryStream = entry.Open();
                await JsonSerializer.SerializeAsync(entryStream, payload, JsonOptions, cancellationToken).ConfigureAwait(false);
            }

            var size = fileSystem.GetFileLength(path);
            if (size <= 0 || size > options.MaximumBundleBytes)
            {
                throw new SupportBundleSizeException();
            }

            var checksum = await fileSystem.ComputeSha256Async(path, cancellationToken).ConfigureAwait(false);
            var artifact = artifacts.Register(
                principalSid,
                "rms-support-bundle.zip",
                path,
                size,
                checksum,
                createdAtUtc);
            return new(artifact, createdAtUtc, correlationId, Sections);
        }
        catch
        {
            try { await fileSystem.DeleteFileAsync(path, CancellationToken.None).ConfigureAwait(false); } catch { }
            throw;
        }
    }

    private static FailureSeverity ToTimelineSeverity(HealthState state) => state switch
    {
        HealthState.Healthy => FailureSeverity.Informational,
        HealthState.Warning => FailureSeverity.Warning,
        HealthState.ActionRequired => FailureSeverity.ActionRequired,
        _ => FailureSeverity.Unknown
    };

    private sealed record SupportBundlePayload(
        string Format,
        DateTimeOffset CreatedAtUtc,
        string CorrelationId,
        IReadOnlyList<string> IncludedSections,
        object Health,
        object Diagnostics,
        object OperationalHealth,
        IReadOnlyList<ServiceFailureAnalysisDto> FailureAnalysis,
        object IncidentTimeline,
        IReadOnlyList<SafeAuditRecord> Audit);

    private sealed record SafeAuditRecord(
        DateTimeOffset AtUtc,
        string Operation,
        string? Target,
        string Outcome,
        string? FailureCode,
        string ProductVersion);

    private static SafeAuditRecord ToSafeAudit(RmsSupportHub.Pos.Domain.Models.AgentAuditEvent item) => new(
        item.AtUtc,
        item.Operation,
        item.Target,
        item.Outcome,
        item.FailureCode,
        item.ProductVersion);
}

public sealed class SupportBundleSizeException()
    : InvalidOperationException("The Support Bundle exceeded the server-owned size limit.");
