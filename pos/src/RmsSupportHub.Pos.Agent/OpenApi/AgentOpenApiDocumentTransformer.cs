using System.Net.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace RmsSupportHub.Pos.Agent;

/// <summary>
/// Makes the generated document describe the installed browser boundary rather than a framework
/// fallback. Windows Negotiate is represented as HTTP authentication; no bearer/JWT scheme exists.
/// </summary>
public sealed class AgentOpenApiDocumentTransformer : IOpenApiDocumentTransformer
{
    public const string WindowsNegotiateSchemeName = "WindowsNegotiate";

    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Info.Title = "RMS+ POS Agent API";
        document.Info.Version = AgentHostConstants.ApiVersion;
        document.Info.Description =
            "Destination-owned foundation contract for the per-device Windows POS Agent. " +
            "The Agent exposes a fixed HTTPS loopback endpoint for direct Support Hub browser " +
            "access. Windows Negotiate supplies the authenticated identity, local Built-in " +
            "Administrators membership authorizes protected reads and typed Windows-service and RMS " +
            "database operations, and no Support Hub backend relay is involved. The Agent exposes " +
            "read-only device/configuration/RMS visibility plus allow-listed service control and " +
            "server-owned RMS database backup/restore workflows. Downloader, maintenance, and " +
            "artifact capabilities remain typed, principal-scoped, and fail-closed. Mutation tokens are short-lived, " +
            "one-use, and bound to the authenticated principal, exact Origin, registered operation, " +
            "target, method, and path.";
        document.Servers = [new OpenApiServer
        {
            Url = AgentHostConstants.CanonicalOrigin,
            Description = "Fixed HTTPS loopback Agent origin"
        }];

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??=
            new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.Ordinal);
        document.Components.SecuritySchemes[WindowsNegotiateSchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "negotiate",
            Description =
                "Windows Integrated Authentication using Negotiate. The browser/OS supplies " +
                "credentials with a credentialed cross-origin request; clients must rely on the " +
                "browser's integrated credential flow."
        };

        AddWindowsSecurityRequirement(document, "/api/v1/session", HttpMethod.Get);
        AddWindowsSecurityRequirement(document, "/api/v1/security/mutation-token", HttpMethod.Post);
        AddWindowsSecurityRequirement(document, "/api/v1/device/identity", HttpMethod.Get);
        AddWindowsSecurityRequirement(document, "/api/v1/device/connectivity", HttpMethod.Get);
        AddWindowsSecurityRequirement(document, "/api/v1/device/capabilities", HttpMethod.Get);
        AddWindowsSecurityRequirement(document, "/api/v1/configuration", HttpMethod.Get);
        AddWindowsSecurityRequirement(document, "/api/v1/services", HttpMethod.Get);
        AddWindowsSecurityRequirement(document, "/api/v1/services/{serviceId}/actions", HttpMethod.Post);
        AddWindowsSecurityRequirement(document, "/api/v1/rms/diagnostics", HttpMethod.Get);
        AddWindowsSecurityRequirement(document, "/api/v1/health/check", HttpMethod.Get);
        AddWindowsSecurityRequirement(document, "/api/v1/diagnostics/services/{serviceId}/failure", HttpMethod.Get);
        AddWindowsSecurityRequirement(document, "/api/v1/diagnostics/timeline", HttpMethod.Get);
        AddWindowsSecurityRequirement(document, "/api/v1/support-bundles", HttpMethod.Post);
        AddWindowsSecurityRequirement(document, "/api/v1/rms/databases/{targetId}", HttpMethod.Get);
        AddWindowsSecurityRequirement(document, "/api/v1/rms/databases/{targetId}/backup", HttpMethod.Post);
        AddWindowsSecurityRequirement(document, "/api/v1/rms/databases/{targetId}/restore", HttpMethod.Post);
        AddWindowsSecurityRequirement(document, "/api/v1/rms/databases/{targetId}/operations/{operationId}", HttpMethod.Get);
        AddWindowsSecurityRequirement(document, "/api/v1/rms/databases/{targetId}/operations/{operationId}/events", HttpMethod.Get);
        AddWindowsSecurityRequirement(document, "/api/v1/downloads/branches", HttpMethod.Get);
        AddWindowsSecurityRequirement(document, "/api/v1/downloads/batches", HttpMethod.Post);
        AddWindowsSecurityRequirement(document, "/api/v1/downloads/operations/{operationId}", HttpMethod.Get);
        AddWindowsSecurityRequirement(document, "/api/v1/downloads/operations/{operationId}/events", HttpMethod.Get);
        AddWindowsSecurityRequirement(document, "/api/v1/maintenance/cleanup/preview", HttpMethod.Post);
        AddWindowsSecurityRequirement(document, "/api/v1/maintenance/cleanup/execute", HttpMethod.Post);
        AddWindowsSecurityRequirement(document, "/api/v1/maintenance/reset/preview", HttpMethod.Post);
        AddWindowsSecurityRequirement(document, "/api/v1/maintenance/reset/execute", HttpMethod.Post);
        AddWindowsSecurityRequirement(document, "/api/v1/maintenance/operations/{operationId}", HttpMethod.Get);
        AddWindowsSecurityRequirement(document, "/api/v1/maintenance/operations/{operationId}/events", HttpMethod.Get);
        AddWindowsSecurityRequirement(document, "/api/v1/artifacts/{artifactId}", HttpMethod.Get);
        AddReferencePropertyDescriptions(document);

        return Task.CompletedTask;
    }

    private static void AddReferencePropertyDescriptions(OpenApiDocument document)
    {
        SetSchemaDescription(document, "HealthReportDto", "Aggregate read-only POS health report composed from bounded local evidence.");
        SetSchemaDescription(document, "HealthCheckDto", "One bounded read-only health result with an explicit unknown state.");
        SetSchemaDescription(document, "RmsComponentDriftDto", "Server-owned comparison between an installed RMS component build and Product Release.");
        SetSchemaDescription(document, "RmsDatabaseHealthDto", "Typed database backup and storage health evidence for one approved RMS database target.");
        SetSchemaDescription(document, "RmsDatabaseBackupHealthDto", "Bounded inventory and freshness evidence for Agent-approved database backups.");
        SetSchemaDescription(document, "RmsStorageHealthDto", "Bounded capacity evidence for the Agent-approved database storage root.");
        SetSchemaDescription(document, "ServiceFailureAnalysisDto", "Typed, bounded, non-mutating analysis for one opaque RMS service identifier.");
        SetSchemaDescription(document, "FailureEvidenceDto", "One redacted exception, event, or bounded log evidence item.");
        SetSchemaDescription(document, "FailureRecommendationDto", "Non-executing remediation guidance derived from bounded failure evidence.");
        SetSchemaDescription(document, "IncidentTimelineDto", "Bounded principal-scoped timeline used for local incident correlation.");
        SetSchemaDescription(document, "IncidentTimelineEventDto", "One safe event in the bounded principal-scoped incident timeline.");
        SetSchemaDescription(document, "SupportBundleDto", "Opaque result describing a generated, redacted Support Bundle artifact.");

        SetPropertyDescription(
            document,
            "DeviceConnectivityDto",
            "localSql",
            "Independent local SQL endpoint reachability evidence.");
        SetPropertyDescription(
            document,
            "DeviceConnectivityDto",
            "mainServer",
            "Independent main-server endpoint reachability evidence.");
        SetPropertyDescription(document, "DeviceIdentityDto", "productRelease", "Product Release read from the fixed release file.");
        SetPropertyDescription(
            document,
            "EvidenceDto",
            "freshness",
            "Freshness classification for this observation; it is never collapsed into a bare online boolean.");
        SetPropertyDescription(
            document,
            "RedactedConfigurationDto",
            "downloader",
            "Redacted DB Downloader settings with secret-presence metadata only.");
        SetPropertyDescription(
            document,
            "ServiceSummaryDto",
            "state",
            "Current Windows service runtime state observed by the Agent.");
        SetPropertyDescription(
            document,
            "ServiceSummaryDto",
            "lastChecked",
            "Freshness and check-time evidence for the service state.");
        SetPropertyDescription(
            document,
            "ServiceSummaryDto",
            "allowedActions",
            "Typed service actions currently valid for the observed state; the server owns this list.");
        SetPropertyDescription(
            document,
            "ServiceActionResponseDto",
            "outcome",
            "Typed service-action outcome truth, independent of the HTTP status.");
        SetPropertyDescription(
            document,
            "ServiceActionRequestDto",
            "action",
            "One of the explicit Start, Stop, or Restart operations supported by the Agent.");
        SetPropertyDescription(
            document,
            "ServiceActionRequestDto",
            "idempotencyKey",
            "Bounded caller-generated key scoped to the opaque service identifier; repeating the same key and action returns the original typed response without a second dispatch.");
        SetPropertyDescription(
            document,
            "ServiceActionResponseDto",
            "detail",
            "Safe operator-facing detail without exception, identity, credential, path, command, or raw target disclosure.");
        SetPropertyDescription(
            document,
            "RmsDiagnosticsDto",
            "installation",
            "Safe installed RMS identity and consistency evidence.");
        SetPropertyDescription(
            document,
            "RmsDiagnosticsDto",
            "connectivity",
            "Safe main-server and Branch-server reachability evidence.");
        SetPropertyDescription(
            document,
            "RmsDiagnosticsDto",
            "branchDatabase",
            "Sanitized Branch database diagnostic for RmsBranchSrv.");
        SetPropertyDescription(
            document,
            "RmsDiagnosticsDto",
            "cashierDatabase",
            "Sanitized Cashier database diagnostic for RmsCashierSrv.");
        SetPropertyDescription(
            document,
            "RmsDiagnosticsDto",
            "services",
            "Current SCM status for the bounded canonical RMS service catalog.");
        SetPropertyDescription(document, "RmsInstallationDto", "productRelease", "Product Release read only from the fixed C:\\ProgramData\\RMS_Plus\\ReleaseNumber.txt file.");
        SetPropertyDescription(document, "RmsInstallationDto", "componentDrift", "Bounded per-component comparison against Product Release.");
        SetPropertyDescription(
            document,
            "RmsInstallationDto",
            "versions",
            "Build metadata for the installed RMS components.");
        SetPropertyDescription(
            document,
            "RmsInstallationDto",
            "consistency",
            "Cross-file consistency evidence for duplicated RMS values.");
        SetPropertyDescription(document, "RmsConsistencyDto", "branchCode", "Comparison status for duplicated BranchCode values.");
        SetPropertyDescription(document, "RmsConsistencyDto", "posIdentity", "Comparison status for duplicated POS identity values.");
        SetPropertyDescription(document, "RmsConsistencyDto", "mainServerBranchId", "Comparison status for duplicated main-server branch identifiers.");
        SetPropertyDescription(document, "RmsConsistencyDto", "mainServerPosId", "Comparison status for duplicated main-server POS identifiers.");
        SetPropertyDescription(document, "RmsConsistencyDto", "version", "Comparison status for installed component build numbers.");
        SetPropertyDescription(document, "RmsConsistencyDto", "warnings", "Safe operator warnings for mismatched or unavailable installed metadata.");
        SetPropertyDescription(document, "RmsVersionDto", "branchServerBuildNumber", "Branch Server BuildNumber selected from its installed appsettings.");
        SetPropertyDescription(document, "RmsVersionDto", "cashierServerBuildNumber", "Cashier Server BuildNumber selected from its installed appsettings.");
        SetPropertyDescription(document, "RmsVersionDto", "cashierUiBuildNumber", "Cashier UI BuildNumber selected from its installed appsettings.");
        SetPropertyDescription(document, "RmsEndpointDiagnosticDto", "configured", "Whether a valid known endpoint was configured.");
        SetPropertyDescription(document, "RmsEndpointDiagnosticDto", "endpoint", "Sanitized endpoint host and port; credentials and paths are omitted.");
        SetPropertyDescription(document, "RmsEndpointDiagnosticDto", "reachability", "TCP reachability evidence only; application health is not inferred.");
        SetPropertyDescription(document, "RmsConnectivityDto", "mainServer", "Main-server configuration and reachability evidence.");
        SetPropertyDescription(document, "RmsConnectivityDto", "branchServer", "Branch-server configuration and reachability evidence.");
        SetPropertyDescription(document, "RmsDatabaseDiagnosticDto", "expectedDatabase", "Canonical database name expected for this RMS component.");
        SetPropertyDescription(document, "RmsDatabaseDiagnosticDto", "configuredDatabase", "Database name parsed from the installed RMS connection string, without returning the string itself.");
        SetPropertyDescription(document, "RmsDatabaseDiagnosticDto", "serverDisplay", "Safe SQL data-source label parsed from the installed RMS connection string.");
        SetPropertyDescription(document, "RmsDatabaseDiagnosticDto", "configured", "Whether the known RMS connection-string setting is present.");
        SetPropertyDescription(document, "RmsDatabaseDiagnosticDto", "databaseNameMatches", "Whether the configured/queried database identity matches the canonical expected database.");
        SetPropertyDescription(document, "RmsDatabaseDiagnosticDto", "connectivityStatus", "Sanitized result of configuration validation and the fixed read-only SQL probe.");
        SetPropertyDescription(document, "RmsDatabaseDiagnosticDto", "evidence", "Freshness and safe detail for the database diagnostic.");
        SetPropertyDescription(document, "RmsDatabaseDiagnosticDto", "health", "Bounded approved-backup and fixed-root storage health evidence.");
        SetPropertyDescription(document, "RmsDatabaseHealthDto", "backups", "Approved backup inventory and freshness evidence.");
        SetPropertyDescription(document, "RmsDatabaseHealthDto", "storage", "Fixed-root capacity evidence without exposing a path.");
        SetPropertyDescription(document, "RmsDatabaseBackupHealthDto", "count", "Number of physically valid Agent-approved backups.");
        SetPropertyDescription(document, "RmsDatabaseBackupHealthDto", "latestCreatedAtUtc", "Creation time of the newest approved backup, when available.");
        SetPropertyDescription(document, "RmsDatabaseBackupHealthDto", "freshness", "Freshness classification of the newest approved backup.");
        SetPropertyDescription(document, "RmsDatabaseBackupHealthDto", "state", "Health classification of the approved backup inventory.");
        SetPropertyDescription(document, "RmsDatabaseBackupHealthDto", "summary", "Safe backup inventory summary.");
        SetPropertyDescription(document, "RmsStorageHealthDto", "state", "Health classification of the approved storage root.");
        SetPropertyDescription(document, "RmsStorageHealthDto", "availableFreeSpaceBytes", "Available bytes reported by the approved storage provider.");
        SetPropertyDescription(document, "RmsStorageHealthDto", "rootAvailable", "Whether the approved storage root was available as a directory.");
        SetPropertyDescription(document, "RmsStorageHealthDto", "freshness", "Freshness of the capacity observation.");
        SetPropertyDescription(document, "RmsStorageHealthDto", "summary", "Safe capacity summary.");
        SetPropertyDescription(document, "HealthReportDto", "overallState", "Conservative aggregate state across the returned checks.");
        SetPropertyDescription(document, "HealthReportDto", "summary", "Safe aggregate health summary.");
        SetPropertyDescription(document, "HealthReportDto", "checkedAtUtc", "UTC time at which this health report was evaluated.");
        SetPropertyDescription(document, "HealthReportDto", "checks", "Bounded individual health checks.");
        SetPropertyDescription(document, "HealthCheckDto", "code", "Stable server-owned health check code.");
        SetPropertyDescription(document, "HealthCheckDto", "state", "Current health classification.");
        SetPropertyDescription(document, "HealthCheckDto", "summary", "Safe operator summary.");
        SetPropertyDescription(document, "HealthCheckDto", "checkedAtUtc", "UTC time at which the check was evaluated.");
        SetPropertyDescription(document, "HealthCheckDto", "remediation", "Optional non-executing next-step guidance.");
        SetPropertyDescription(document, "RmsComponentDriftDto", "component", "Server-owned RMS component label.");
        SetPropertyDescription(document, "RmsComponentDriftDto", "buildNumber", "Installed component build number.");
        SetPropertyDescription(document, "RmsComponentDriftDto", "productRelease", "Product Release from the fixed release file.");
        SetPropertyDescription(document, "RmsComponentDriftDto", "state", "Component comparison state.");
        SetPropertyDescription(document, "RmsComponentDriftDto", "reason", "Safe comparison reason.");
        SetPropertyDescription(document, "ServiceFailureAnalysisDto", "evidence", "Bounded redacted failure evidence.");
        SetPropertyDescription(document, "ServiceFailureAnalysisDto", "unknownReasons", "Safe reasons evidence may be incomplete.");
        SetPropertyDescription(document, "ServiceFailureAnalysisDto", "recommendations", "Non-executing operator guidance.");
        SetPropertyDescription(document, "FailureEvidenceDto", "summary", "Redacted bounded evidence summary.");
        SetPropertyDescription(document, "FailureEvidenceDto", "stackFrames", "Redacted bounded stack-frame labels.");
        SetPropertyDescription(document, "ServiceFailureAnalysisDto", "serviceId", "Opaque server-owned service identifier.");
        SetPropertyDescription(document, "ServiceFailureAnalysisDto", "serviceDisplayName", "Safe service display name.");
        SetPropertyDescription(document, "ServiceFailureAnalysisDto", "category", "Selected failure category.");
        SetPropertyDescription(document, "ServiceFailureAnalysisDto", "severity", "Conservative failure severity.");
        SetPropertyDescription(document, "ServiceFailureAnalysisDto", "confidence", "Confidence that bounded evidence supports the classification.");
        SetPropertyDescription(document, "ServiceFailureAnalysisDto", "summary", "Safe operator summary.");
        SetPropertyDescription(document, "ServiceFailureAnalysisDto", "checkedAtUtc", "UTC time at which the analysis was evaluated.");
        SetPropertyDescription(document, "FailureEvidenceDto", "source", "Safe evidence source label.");
        SetPropertyDescription(document, "FailureEvidenceDto", "atUtc", "UTC evidence time, when supplied by the source.");
        SetPropertyDescription(document, "FailureEvidenceDto", "exceptionType", "Exception type only, without a message or assembly path.");
        SetPropertyDescription(document, "FailureEvidenceDto", "eventId", "Safe provider event identifier, when available.");
        SetPropertyDescription(document, "FailureRecommendationDto", "code", "Stable recommendation code.");
        SetPropertyDescription(document, "FailureRecommendationDto", "label", "Operator-facing recommendation label.");
        SetPropertyDescription(document, "FailureRecommendationDto", "summary", "Safe reason for the recommendation.");
        SetPropertyDescription(document, "IncidentTimelineDto", "events", "Newest-first bounded principal-scoped timeline events.");
        SetPropertyDescription(document, "IncidentTimelineDto", "unknownReasons", "Safe reasons historical evidence may be incomplete.");
        SetPropertyDescription(document, "IncidentTimelineDto", "generatedAtUtc", "UTC time at which this timeline view was generated.");
        SetPropertyDescription(document, "IncidentTimelineEventDto", "eventId", "Opaque timeline event identifier.");
        SetPropertyDescription(document, "IncidentTimelineEventDto", "atUtc", "UTC time at which the event was recorded.");
        SetPropertyDescription(document, "IncidentTimelineEventDto", "kind", "Safe server-owned event kind.");
        SetPropertyDescription(document, "IncidentTimelineEventDto", "severity", "Conservative event severity.");
        SetPropertyDescription(document, "IncidentTimelineEventDto", "summary", "Redacted operator summary.");
        SetPropertyDescription(document, "IncidentTimelineEventDto", "serviceId", "Opaque service identifier, when the event concerns a service.");
        SetPropertyDescription(document, "IncidentTimelineEventDto", "operationId", "Safe server-owned operation identifier, when applicable.");
        SetPropertyDescription(document, "IncidentTimelineEventDto", "correlationId", "Safe correlation identifier, when available.");
        SetPropertyDescription(document, "SupportBundleDto", "artifact", "Opaque principal-scoped Support Bundle artifact capability.");
        SetPropertyDescription(document, "SupportBundleDto", "createdAtUtc", "UTC time at which the Support Bundle was created.");
        SetPropertyDescription(document, "SupportBundleDto", "correlationId", "Safe request correlation identifier.");
        SetPropertyDescription(document, "SupportBundleDto", "includedSections", "Server-owned sections included in the archive.");
        SetPropertyDescription(document, "RmsDatabaseArtifactDto", "artifactId", "Opaque Agent-owned backup handle; it is not a filesystem path or download URL.");
        SetPropertyDescription(document, "RmsDatabaseArtifactDto", "displayName", "Server-generated safe backup display name without a physical path.");
        SetPropertyDescription(document, "RmsDatabaseOperationDto", "detail", "Safe operator detail without credentials, connection strings, SQL, unrestricted paths, or raw service targets.");
        SetPropertyDescription(document, "RmsDatabaseOperationDto", "recoveryRequired", "Whether the Agent cannot prove that database/service recovery is complete and inspection is required before retrying.");
        SetPropertyDescription(document, "RmsDatabaseOperationDto", "target", "Server-owned Branch or Cashier database target.");
        SetPropertyDescription(document, "RmsDatabaseOperationDto", "operation", "Typed Backup or Restore operation selected by the server route.");
        SetPropertyDescription(document, "RmsDatabaseOperationDto", "state", "Current REST/SSE lifecycle state.");
        SetPropertyDescription(document, "RmsDatabaseOperationDto", "outcome", "Typed operation outcome truth; OutcomeUnknown requires inspection before retry.");
        SetPropertyDescription(document, "RmsDatabaseOperationDto", "artifact", "Sanitized approved artifact metadata, when the workflow produced or used one.");
        SetPropertyDescription(document, "RmsDatabaseWorkspaceDto", "approvedBackups", "Sanitized metadata for backup artifacts registered by this Agent instance and target.");
        SetPropertyDescription(document, "RmsDatabaseWorkspaceDto", "target", "Server-owned Branch or Cashier database target.");
        SetPropertyDescription(document, "RmsDatabaseWorkspaceDto", "latestOperation", "Latest principal-scoped operation state, when retained.");
        SetPropertyDescription(document, "DownloaderBranchOutcomeDto", "state", "Sanitized branch download lifecycle state.");
        SetPropertyDescription(document, "DownloaderOperationOutcomeDto", "triggerState", "Truth about the server-side backup trigger milestone.");
        SetPropertyDescription(document, "DownloaderOperationDto", "state", "Current downloader REST/SSE lifecycle state.");
        SetPropertyDescription(document, "DownloaderOperationDto", "outcome", "Downloader outcome truth; OutcomeUnknown requires inspection before retry.");
        SetPropertyDescription(document, "DownloaderOperationDto", "downloaderOutcome", "Sanitized branch and trigger outcome evidence, when available.");
        SetPropertyDescription(document, "MaintenanceItemOutcomeDto", "state", "Sanitized maintenance item lifecycle state.");
        SetPropertyDescription(document, "MaintenanceOperationDto", "state", "Current maintenance REST/SSE lifecycle state.");
        SetPropertyDescription(document, "MaintenanceOperationDto", "outcome", "Maintenance outcome truth; OutcomeUnknown requires recovery inspection before retry.");
        SetPropertyDescription(document, "MaintenanceOperationDto", "maintenanceOutcome", "Sanitized logical item outcomes and recovery truth, when available.");
        SetPropertyDescription(document, "MainServerProfileDto", "environment", "Server-owned Main Server environment selection; Production remains disabled until explicitly enabled by the server.");
        SetPropertyDescription(document, "MainServerProfileDto", "binding", "Server-owned Branch/POS binding state; the browser cannot provide a host, branch, POS, or endpoint override.");
        SetPropertyDescription(document, "MainServerProfilesDto", "activeBinding", "Server-owned Branch/POS binding state for the active profile; the browser cannot provide a host, branch, POS, or endpoint override.");
        SetPropertyDescription(document, "MainServerStateEvidenceDto", "environment", "Server-owned Main Server environment associated with this state observation.");
        SetPropertyDescription(document, "MainServerStateEvidenceDto", "binding", "Server-owned Branch/POS binding associated with this state observation.");
        SetPropertyDescription(document, "MainServerStateEvidenceDto", "outcome", "Typed Main Server read outcome; unavailable or ambiguous responses remain explicit.");
        SetPropertyDescription(document, "SafetySnapshotPreviewDto", "evidenceState", "Typed pre-maintenance evidence availability classification; unavailable evidence blocks repair.");
        SetPropertyDescription(document, "SafetySnapshotDto", "state", "Typed Safety Snapshot lifecycle state.");
        SetPropertyDescription(document, "SafetySnapshotDto", "evidenceState", "Typed pre-maintenance evidence availability classification.");
        SetPropertyDescription(document, "SafetySnapshotDto", "capacity", "Bounded storage-capacity evidence captured by the Agent.");
        SetPropertyDescription(document, "SafetySnapshotDto", "backups", "Bounded approved-backup evidence captured by the Agent.");
        SetPropertyDescription(document, "SafetySnapshotVerificationDto", "state", "Typed Safety Snapshot verification state; stale, mismatched, or unverifiable evidence remains non-authorizing.");
        SetPropertyDescription(document, "DiagnosticConsolePreviewDto", "target", "Fixed server-owned diagnostic manifest target; no browser-selected executable or arguments are accepted.");
        SetPropertyDescription(document, "DiagnosticConsolePreviewDto", "state", "Typed diagnostic preview state.");
        SetPropertyDescription(document, "DiagnosticConsoleRunDto", "target", "Fixed server-owned diagnostic manifest target used by this operation.");
        SetPropertyDescription(document, "DiagnosticConsoleRunDto", "state", "Typed diagnostic operation lifecycle state.");
        SetPropertyDescription(document, "DiagnosticConsoleRunDto", "outcome", "Typed diagnostic operation outcome truth.");
        SetPropertyDescription(document, "DiagnosticConsoleRunDto", "result", "Separate bounded stdout/stderr artifact metadata, when a result exists.");
        SetPropertyDescription(document, "AgentPackageStatusDto", "verification", "Typed package signature, checksum, compatibility, and trust verification state.");
        SetPropertyDescription(document, "AgentPackageStatusDto", "state", "Typed installed package lifecycle state.");
        SetPropertyDescription(document, "AgentPackageStatusDto", "manifest", "Safe verified package manifest projection, when available.");
        SetPropertyDescription(document, "AgentPackagePreviewDto", "operation", "Typed package lifecycle operation selected by the server-owned policy.");
        SetPropertyDescription(document, "AgentPackagePreviewDto", "verification", "Typed package signature, checksum, compatibility, and trust verification state.");
        SetPropertyDescription(document, "AgentPackagePreviewDto", "manifest", "Safe verified package manifest projection, when available.");
        SetPropertyDescription(document, "AgentPackageOperationDto", "operation", "Typed package lifecycle operation selected by the server-owned policy.");
        SetPropertyDescription(document, "AgentPackageOperationDto", "state", "Typed package lifecycle state.");
        SetPropertyDescription(document, "AgentPackageOperationDto", "outcome", "Typed package lifecycle outcome truth.");
        SetPropertyDescription(document, "RepairPreviewDto", "operation", "Typed repair operation selected by the server-owned policy.");
        SetPropertyDescription(document, "RepairPreviewDto", "packageVerification", "Typed package verification state required before repair can proceed.");
        SetPropertyDescription(document, "RepairPreviewDto", "snapshot", "Reference to the fresh principal-scoped Safety Snapshot required by this repair preview.");
        SetPropertyDescription(document, "RepairOperationDto", "operation", "Typed repair operation selected by the server-owned policy.");
        SetPropertyDescription(document, "RepairOperationDto", "state", "Typed repair lifecycle state.");
        SetPropertyDescription(document, "RepairOperationDto", "outcome", "Typed repair lifecycle outcome truth.");
        SetPropertyDescription(document, "GuidedRepairDto", "state", "Typed Guided Repair workflow state; checkpoint progress never implies package activation.");
        SetPropertyDescription(document, "GuidedRepairStepDto", "state", "Typed Guided Repair checkpoint state.");
    }

    private static void SetSchemaDescription(
        OpenApiDocument document,
        string schemaName,
        string description)
    {
        if (document.Components?.Schemas is null
            || !document.Components.Schemas.TryGetValue(schemaName, out var schema))
        {
            return;
        }

        switch (schema)
        {
            case OpenApiSchema openApiSchema:
                openApiSchema.Description = description;
                break;
            case OpenApiSchemaReference schemaReference:
                schemaReference.Description = description;
                break;
        }
    }

    private static void SetPropertyDescription(
        OpenApiDocument document,
        string schemaName,
        string propertyName,
        string description)
    {
        if (document.Components?.Schemas is null
            || !document.Components.Schemas.TryGetValue(schemaName, out var schema)
            || schema is not OpenApiSchema openApiSchema
            || openApiSchema.Properties is null
            || !openApiSchema.Properties.TryGetValue(propertyName, out var property)
            )
        {
            return;
        }

        switch (property)
        {
            case OpenApiSchema propertySchema:
                propertySchema.Description = description;
                break;
            case OpenApiSchemaReference propertyReference:
                propertyReference.Description = description;
                break;
        }
    }

    private static void AddWindowsSecurityRequirement(
        OpenApiDocument document,
        string path,
        HttpMethod method)
    {
        if (!document.Paths.TryGetValue(path, out var pathItem) || pathItem is null)
        {
            return;
        }

        var operations = pathItem.Operations;
        if (operations is null || !operations.TryGetValue(method, out var operation) || operation is null)
        {
            return;
        }

        operation.Security =
        [
            new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(
                    WindowsNegotiateSchemeName,
                    document,
                    externalResource: null)] = []
            }
        ];
    }
}
