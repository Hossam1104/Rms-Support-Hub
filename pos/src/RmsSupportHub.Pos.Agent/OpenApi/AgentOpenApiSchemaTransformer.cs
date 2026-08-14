using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using RmsSupportHub.Pos.Contracts.V1.Common;
using RmsSupportHub.Pos.Contracts.V1.Configuration;
using RmsSupportHub.Pos.Contracts.V1.Device;
using RmsSupportHub.Pos.Contracts.V1.Downloader;
using RmsSupportHub.Pos.Contracts.V1.Maintenance;
using RmsSupportHub.Pos.Contracts.V1.Rms;
using RmsSupportHub.Pos.Contracts.V1.Security;
using RmsSupportHub.Pos.Contracts.V1.Services;
using RmsSupportHub.Pos.Contracts.V1.Session;

namespace RmsSupportHub.Pos.Agent;

/// <summary>
/// Adds concise, security-aware descriptions to schemas and properties reachable from the current
/// Agent operations.
/// </summary>
public sealed class AgentOpenApiSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        var type = context.JsonPropertyInfo?.DeclaringType ?? context.JsonTypeInfo.Type;
        var propertyName = context.JsonPropertyInfo?.Name;

        schema.Description = (type, propertyName) switch
        {
            (var value, null) when value == typeof(HealthStatusDto) =>
                "Anonymous health response produced by the Agent. Status identifies the foundation " +
                "check that answered; it contains no machine or credential detail.",
            (var value, null) when value == typeof(SessionInfoDto) =>
                "Security and API-version diagnostics produced by the Agent for the authenticated " +
                "Windows account. The raw Windows SID is intentionally omitted.",
            (var value, null) when value == typeof(MutationTokenIssueRequestDto) =>
                "Browser request for a token for one logical operation known to the server. For a " +
                "target-bound operation, targetId is an opaque server-issued identifier; the browser " +
                "cannot supply the target HTTP method or path.",
            (var value, null) when value == typeof(MutationTokenIssueResponseDto) =>
                "Opaque, short-lived mutation-token response produced by the Agent. The token is " +
                "intended for browser memory only.",
            (var value, null) when value == typeof(AgentProblemDetailsDto) =>
                "Safe application/problem+json error contract. Code is a stable problem code and " +
                "correlationId may identify the request; sensitive identity and machine details are " +
                "not included.",
            (var value, null) when value == typeof(DeviceIdentityDto) =>
                "Safe server-owned branch, POS, release, and client identity. It contains no Windows " +
                "SID, credential, or unrestricted host path.",
            (var value, null) when value == typeof(DeviceConnectivityDto) =>
                "Independent local SQL and main-server reachability evidence. Reachable TCP does not " +
                "claim that SQL or application health was queried.",
            (var value, null) when value == typeof(DeviceCapabilitiesDto) =>
                "Non-secret capability metadata for the installed Agent. Browse roots expose display " +
                "metadata only and do not expose host paths.",
            (var value, null) when value == typeof(BrowseRootDto) =>
                "Safe browse-root display metadata. The corresponding host path remains server-owned.",
            (var value, null) when value == typeof(RedactedConfigurationDto) =>
                "Service-owned POS configuration with secret-presence flags. Password values, protected " +
                "data, backup paths, and configuration source paths are never returned.",
            (var value, null) when value == typeof(RedactedDownloaderConfigurationDto) =>
                "Non-secret DB Downloader settings with an RDB password-presence flag. Passwords and " +
                "SMB/UNC paths are never returned.",
            (var value, null) when value == typeof(ServiceSummaryDto) =>
                "Allow-listed Windows service visibility, current status evidence, and the typed " +
                "service actions valid for the observed state.",
            (var value, null) when value == typeof(ServiceActionRequestDto) =>
                "Typed Start, Stop, or Restart request for one opaque allow-listed service. It " +
                "contains no raw service name, host path, command, SQL, script, or executable input.",
            (var value, null) when value == typeof(ServiceActionResponseDto) =>
                "Safe result for one service action. Outcome truth is separate from HTTP status and " +
                "contains no exception, SID, credential, path, command, or raw service target.",
            (var value, null) when value == typeof(RmsDiagnosticsDto) =>
                "Sanitized read-only RMS installation, connectivity, database, and canonical service " +
                "diagnostics. Credentials, connection strings, keys, and unrestricted targets are " +
                "never returned.",
            (var value, null) when value == typeof(RmsInstallationDto) =>
                "Safe identity and consistency evidence selected from the installed RMS+ files.",
            (var value, null) when value == typeof(RmsVersionDto) =>
                "Build metadata selected from the installed RMS server and UI configuration files.",
            (var value, null) when value == typeof(RmsConsistencyDto) =>
                "Typed comparison results for duplicated installed RMS metadata.",
            (var value, null) when value == typeof(RmsEndpointDiagnosticDto) =>
                "Safe endpoint configuration and TCP reachability evidence; it does not claim application health.",
            (var value, null) when value == typeof(RmsConnectivityDto) =>
                "Independent main-server and Branch-server endpoint reachability evidence.",
            (var value, null) when value == typeof(RmsDatabaseDiagnosticDto) =>
                "Sanitized read-only RMS database configuration and identity-probe result.",
            (var value, null) when value == typeof(RmsDatabaseArtifactDto) =>
                "Sanitized metadata for an approved Agent-owned RMS database backup; the physical " +
                "filesystem path and download URL are intentionally absent.",
            (var value, null) when value == typeof(RmsDatabaseBackupRequestDto) =>
                "Typed RMS database backup request containing only a bounded idempotency key.",
            (var value, null) when value == typeof(RmsDatabaseRestoreRequestDto) =>
                "Typed destructive RMS database restore request containing only an opaque approved " +
                "artifact ID, exact target confirmation, and bounded idempotency key.",
            (var value, null) when value == typeof(RmsDatabaseOperationDto) =>
                "Sanitized REST/SSE truth for one principal-scoped RMS database operation, including " +
                "progress, result, artifact metadata, and recovery status without credentials, SQL, " +
                "or unrestricted paths.",
            (var value, null) when value == typeof(RmsDatabaseWorkspaceDto) =>
                "Sanitized Branch or Cashier RMS database workspace with approved backup metadata and " +
                "the latest principal-scoped operation.",
            (var value, null) when value == typeof(RmsDatabaseTarget) =>
                "Server-owned RMS database target: Branch or Cashier.",
            (var value, null) when value == typeof(RmsDatabaseOperationKind) =>
                "Typed RMS database operation kind: Backup or Restore.",
            (var value, null) when value == typeof(RmsDatabaseOperationState) =>
                "Lifecycle state for a typed RMS database operation.",
            (var value, null) when value == typeof(RmsDatabaseOperationOutcome) =>
                "Outcome truth for a typed RMS database operation; ambiguous outcomes are never retried automatically.",
            (var value, null) when value == typeof(ServiceActionOutcome) =>
                "Typed service-action outcome: NotAttempted, Failed, Accepted, or OutcomeUnknown.",
            (var value, null) when value == typeof(EvidenceDto) =>
                "A bounded status observation with explicit freshness and UTC check time.",
            (var value, null) when value == typeof(BranchCatalogEntryDto) =>
                "Server-approved downloader branch metadata. It contains no SMB path, credential, or endpoint detail.",
            (var value, null) when value == typeof(TriggerBatchRequestDto) =>
                "Typed downloader batch request containing only server-approved logical branch codes and a bounded idempotency key.",
            (var value, null) when value == typeof(DownloaderBranchOutcomeDto) =>
                "Sanitized outcome for one requested downloader branch; artifactId is an opaque Agent capability.",
            (var value, null) when value == typeof(DownloaderOperationOutcomeDto) =>
                "Downloader trigger and branch outcome truth without remote paths, credentials, or exception text.",
            (var value, null) when value == typeof(DownloaderOperationDto) =>
                "Principal-scoped downloader progress and outcome with opaque operation and artifact identifiers.",
            (var value, null) when value == typeof(CleanupExecuteRequestDto) =>
                "Typed cleanup execution request containing only a preview challenge, exact confirmation, and bounded idempotency key.",
            (var value, null) when value == typeof(BranchResetExecuteRequestDto) =>
                "Typed branch-reset execution request containing only a preview challenge, exact confirmation, and bounded idempotency key.",
            (var value, null) when value == typeof(CleanupPreviewDto) =>
                "Server-owned cleanup impact preview with logical target identifiers and an expiring principal-bound challenge.",
            (var value, null) when value == typeof(CleanupTargetPreviewDto) =>
                "Sanitized cleanup target evidence identified by a logical target ID; host paths never cross the Agent boundary.",
            (var value, null) when value == typeof(BranchResetPreviewDto) =>
                "Server-owned branch-reset scope preview with approved logical table scope and an expiring challenge.",
            (var value, null) when value == typeof(BranchResetTablePreviewDto) =>
                "Sanitized branch-reset table evidence from the server-owned read-only scope check.",
            (var value, null) when value == typeof(MaintenancePolicyRejectionDto) =>
                "Sanitized maintenance policy rejection containing a logical target ID and stable code, never a host path.",
            (var value, null) when value == typeof(MaintenanceItemOutcomeDto) =>
                "Sanitized maintenance item outcome with logical target identity and explicit recovery truth.",
            (var value, null) when value == typeof(MaintenanceOperationOutcomeDto) =>
                "Retained cleanup or branch-reset outcome evidence without raw paths, service names, SQL, credentials, or exception text.",
            (var value, null) when value == typeof(MaintenanceOperationDto) =>
                "Principal-scoped maintenance progress and outcome with logical targets and recovery truth.",
            _ => schema.Description
        };

        var propertyDescription = GetPropertyDescription(type, propertyName);
        if (propertyDescription is not null)
        {
            schema.Description = propertyDescription;
        }

        return Task.CompletedTask;
    }

    private static string? GetPropertyDescription(Type type, string? propertyName) =>
        (type, propertyName) switch
        {
            (var value, "status") when value == typeof(HealthStatusDto) =>
                "Identifies which anonymous foundation health check produced the response (live or ready).",
            (var value, "principalName") when value == typeof(SessionInfoDto) =>
                "The OS-provided display name of the authenticated Windows principal. It is produced " +
                "by Negotiate and is not browser-supplied.",
            (var value, "isAuthorized") when value == typeof(SessionInfoDto) =>
                "Whether the authenticated account is a member of the local Built-in Administrators " +
                "group. This is account membership resolved by Windows and is independent of UAC token " +
                "elevation; the browser cannot supply it.",
            (var value, "agentVersion") when value == typeof(SessionInfoDto) =>
                "The installed Agent assembly version produced by the Agent.",
            (var value, "apiVersion") when value == typeof(SessionInfoDto) =>
                "The Agent contract version produced by the Agent.",
            (var value, "supportedApiVersions") when value == typeof(SessionInfoDto) =>
                "Contract versions the installed Agent can serve, produced by the Agent rather than " +
                "accepted from the browser.",
            (var value, "operationId") when value == typeof(MutationTokenIssueRequestDto) =>
                "Stable logical identifier for a server-registered operation. The browser supplies this " +
                "identifier only; it cannot choose the target path or HTTP method.",
            (var value, "targetId") when value == typeof(MutationTokenIssueRequestDto) =>
                "Optional opaque server-issued target identifier. The Agent resolves it through the " +
                "registered operation's allow-list and never treats it as a raw service name or path.",
            (var value, "token") when value == typeof(MutationTokenIssueResponseDto) =>
                "Opaque short-lived, one-use token produced by the Agent. It is bound to the authenticated " +
                "Windows SID, exact Origin, operation, target path, and server-resolved method and should " +
                "remain in browser memory.",
            (var value, "expiresAtUtc") when value == typeof(MutationTokenIssueResponseDto) =>
                "UTC expiry instant enforced by the Agent for the one-use mutation token.",
            (var value, "type") when value == typeof(AgentProblemDetailsDto) =>
                "Problem type identifier for the stable Agent error contract.",
            (var value, "title") when value == typeof(AgentProblemDetailsDto) =>
                "Safe human-readable explanation of why the Agent rejected the request.",
            (var value, "status") when value == typeof(AgentProblemDetailsDto) =>
                "HTTP status code returned by the Agent.",
            (var value, "code") when value == typeof(AgentProblemDetailsDto) =>
                "Optional stable machine-readable Agent problem code used by clients; it never carries " +
                "raw identity or credential data.",
            (var value, "correlationId") when value == typeof(AgentProblemDetailsDto) =>
                "Optional request correlation identifier echoed by the Agent for diagnostics.",
            (var value, "branchCode") when value == typeof(DeviceIdentityDto) =>
                "Server-owned branch code for the local POS device.",
            (var value, "posNumber") when value == typeof(DeviceIdentityDto) =>
                "Server-owned POS number for the local device.",
            (var value, "release") when value == typeof(DeviceIdentityDto) =>
                "Configured RMS+ release label for the local device.",
            (var value, "clientName") when value == typeof(DeviceIdentityDto) =>
                "Configured client/product label for the local device.",
            (var value, "localSql") when value == typeof(DeviceConnectivityDto) =>
                "Independent local SQL endpoint reachability evidence.",
            (var value, "mainServer") when value == typeof(DeviceConnectivityDto) =>
                "Independent main-server endpoint reachability evidence.",
            (var value, "agentVersion") when value == typeof(DeviceCapabilitiesDto) =>
                "Installed Agent assembly version produced by the Agent.",
            (var value, "operatingSystem") when value == typeof(DeviceCapabilitiesDto) =>
                "Operating-system label produced by the Agent.",
            (var value, "browseRoots") when value == typeof(DeviceCapabilitiesDto) =>
                "Safe browse-root display metadata. INT-07 publishes no file-browse capability.",
            (var value, "rootId") when value == typeof(BrowseRootDto) =>
                "Opaque server-owned browse-root identifier.",
            (var value, "displayName") when value == typeof(BrowseRootDto) =>
                "User-facing browse-root label; it is not a host path.",
            (var value, "freshness") when value == typeof(EvidenceDto) =>
                "Freshness classification for this observation; it is never collapsed into a bare online boolean.",
            (var value, "lastCheckedUtc") when value == typeof(EvidenceDto) =>
                "UTC time at which this observation was last checked, when available.",
            (var value, "detail") when value == typeof(EvidenceDto) =>
                "Safe diagnostic detail without credentials, Windows SIDs, or raw exception text.",
            (var value, "branchCode") when value == typeof(BranchCatalogEntryDto) =>
                "Server-approved logical branch code accepted by the typed downloader route.",
            (var value, "isSelected") when value == typeof(BranchCatalogEntryDto) =>
                "Server-owned selection hint; the browser cannot expand the approved branch catalog.",
            (var value, "branchCodes") when value == typeof(TriggerBatchRequestDto) =>
                "Branch codes selected from the server-approved downloader catalog.",
            (var value, "idempotencyKey") when value == typeof(TriggerBatchRequestDto) =>
                "Bounded caller-generated key; repeating the same branch selection returns the retained operation.",
            (var value, "branchCode") when value == typeof(DownloaderBranchOutcomeDto) =>
                "Logical branch code from the server-approved downloader selection.",
            (var value, "state") when value == typeof(DownloaderBranchOutcomeDto) =>
                "Sanitized branch download lifecycle state.",
            (var value, "progressPercent") when value == typeof(DownloaderBranchOutcomeDto) =>
                "Bounded server-reported progress percentage for this branch.",
            (var value, "failureCode") when value == typeof(DownloaderBranchOutcomeDto) =>
                "Stable safe branch failure code, when the branch did not complete.",
            (var value, "artifactId") when value == typeof(DownloaderBranchOutcomeDto) =>
                "Opaque Agent artifact capability, when the branch archive was published.",
            (var value, "branches") when value == typeof(DownloaderOperationOutcomeDto) =>
                "Sanitized outcome for each requested server-approved branch.",
            (var value, "serial") when value == typeof(DownloaderOperationOutcomeDto) =>
                "Safe downloader serial evidence, when available; remote path details are omitted.",
            (var value, "triggerState") when value == typeof(DownloaderOperationOutcomeDto) =>
                "Truth about the server-side backup trigger milestone.",
            (var value, "operatorGuidance") when value == typeof(DownloaderOperationOutcomeDto) =>
                "Safe operator guidance for an incomplete or ambiguous downloader outcome.",
            (var value, "triggerAccepted") when value == typeof(DownloaderOperationOutcomeDto) =>
                "Compatibility projection of triggerState; inspect triggerState for unknown outcomes.",
            (var value, "operationId") when value == typeof(DownloaderOperationDto) =>
                "Opaque downloader operation handle scoped to the authenticated Windows principal.",
            (var value, "state") when value == typeof(DownloaderOperationDto) =>
                "Current downloader REST/SSE lifecycle state.",
            (var value, "outcome") when value == typeof(DownloaderOperationDto) =>
                "Downloader outcome truth; OutcomeUnknown requires inspection before retry.",
            (var value, "progressPercent") when value == typeof(DownloaderOperationDto) =>
                "Bounded server-reported downloader progress percentage.",
            (var value, "stage") when value == typeof(DownloaderOperationDto) =>
                "Server-owned safe downloader workflow stage.",
            (var value, "detail") when value == typeof(DownloaderOperationDto) =>
                "Safe operator detail without SMB paths, credentials, raw exceptions, or transport data.",
            (var value, "startedAtUtc") when value == typeof(DownloaderOperationDto) =>
                "UTC operation start time recorded by the Agent.",
            (var value, "completedAtUtc") when value == typeof(DownloaderOperationDto) =>
                "UTC completion time when the downloader reached a final state.",
            (var value, "downloaderOutcome") when value == typeof(DownloaderOperationDto) =>
                "Sanitized branch and trigger outcome evidence, when available.",
            (var value, "errorCode") when value == typeof(DownloaderOperationDto) =>
                "Stable safe error code, when the downloader did not complete successfully.",
            (var value, "correlationId") when value == typeof(DownloaderOperationDto) =>
                "Safe request correlation identifier for diagnostics.",
            (var value, "challengeId") when value == typeof(CleanupExecuteRequestDto) || value == typeof(BranchResetExecuteRequestDto) =>
                "Opaque, principal-bound preview challenge required for this exact maintenance mode.",
            (var value, "typedConfirmation") when value == typeof(CleanupExecuteRequestDto) || value == typeof(BranchResetExecuteRequestDto) =>
                "Exact confirmation phrase returned by the matching server-owned preview.",
            (var value, "idempotencyKey") when value == typeof(CleanupExecuteRequestDto) || value == typeof(BranchResetExecuteRequestDto) =>
                "Bounded caller-generated key; repeating the same challenge returns the retained operation.",
            (var value, "challengeId") when value == typeof(CleanupPreviewDto) || value == typeof(BranchResetPreviewDto) =>
                "Opaque, principal-bound challenge that expires and can be consumed only once.",
            (var value, "servicesToStop") when value == typeof(CleanupPreviewDto) =>
                "Logical server-owned service target identifiers; raw Windows service names are omitted.",
            (var value, "pathsToDelete") when value == typeof(CleanupPreviewDto) =>
                "Logical server-owned cleanup target identifiers; host paths are never returned.",
            (var value, "confirmationPhrase") when value == typeof(CleanupPreviewDto) || value == typeof(BranchResetPreviewDto) =>
                "Exact phrase required by the matching execute request.",
            (var value, "expiresAtUtc") when value == typeof(CleanupPreviewDto) || value == typeof(BranchResetPreviewDto) =>
                "UTC expiry instant enforced for the one-use preview challenge.",
            (var value, "ready") when value == typeof(CleanupPreviewDto) || value == typeof(BranchResetPreviewDto) =>
                "Whether server-owned policy and read-only scope checks produced an executable preview.",
            (var value, "targets") when value == typeof(CleanupPreviewDto) =>
                "Detailed sanitized cleanup target evidence keyed by logical target ID.",
            (var value, "rejections") when value == typeof(CleanupPreviewDto) || value == typeof(BranchResetPreviewDto) =>
                "Sanitized policy rejections keyed by logical target or scope identifier.",
            (var value, "warnings") when value == typeof(CleanupPreviewDto) || value == typeof(BranchResetPreviewDto) =>
                "Bounded safe preview warnings without raw paths or exception text.",
            (var value, "availableFreeSpaceBytes") when value == typeof(CleanupPreviewDto) || value == typeof(BranchResetPreviewDto) =>
                "Read-only free-space evidence for the server-owned maintenance root, when available.",
            (var value, "branchCode") when value == typeof(BranchResetPreviewDto) =>
                "Server-resolved branch identity used for the approved reset scope.",
            (var value, "affectedTables") when value == typeof(BranchResetPreviewDto) =>
                "Code-owned logical table identifiers approved for the branch reset.",
            (var value, "databaseName") when value == typeof(BranchResetPreviewDto) =>
                "Server-approved database label for the branch reset; no connection string is returned.",
            (var value, "tableScopes") when value == typeof(BranchResetPreviewDto) =>
                "Read-only row-count evidence for the approved logical table scope.",
            (var value, "targetId") when value == typeof(CleanupTargetPreviewDto) || value == typeof(MaintenancePolicyRejectionDto) || value == typeof(MaintenanceItemOutcomeDto) =>
                "Logical server-owned target identifier; it is not a host path or raw service name.",
            (var value, "accepted") when value == typeof(CleanupTargetPreviewDto) =>
                "Whether the configured target passed server cleanup policy.",
            (var value, "exists") when value == typeof(CleanupTargetPreviewDto) =>
                "Whether the approved target existed at preview time.",
            (var value, "isDirectory") when value == typeof(CleanupTargetPreviewDto) =>
                "Whether the approved target is a directory at preview time.",
            (var value, "lengthBytes") when value == typeof(CleanupTargetPreviewDto) =>
                "Read-only target size evidence, when the target is a file.",
            (var value, "childCount") when value == typeof(CleanupTargetPreviewDto) =>
                "Bounded read-only child count, when the target is a directory.",
            (var value, "rejectionCode") when value == typeof(CleanupTargetPreviewDto) =>
                "Stable safe policy rejection code, when the target was rejected.",
            (var value, "tableName") when value == typeof(BranchResetTablePreviewDto) =>
                "Code-owned logical table identifier in the approved branch reset scope.",
            (var value, "matchingRows") when value == typeof(BranchResetTablePreviewDto) =>
                "Read-only row-count evidence for the approved table, when available.",
            (var value, "code") when value == typeof(MaintenancePolicyRejectionDto) =>
                "Stable safe maintenance policy rejection code.",
            (var value, "reason") when value == typeof(MaintenancePolicyRejectionDto) =>
                "Safe operator-facing rejection reason without raw implementation detail.",
            (var value, "kind") when value == typeof(MaintenanceItemOutcomeDto) =>
                "Logical maintenance item kind such as file or database.",
            (var value, "state") when value == typeof(MaintenanceItemOutcomeDto) =>
                "Sanitized maintenance item lifecycle state.",
            (var value, "attempted") when value == typeof(MaintenanceItemOutcomeDto) =>
                "Whether the Agent crossed the corresponding typed mutation seam.",
            (var value, "completed") when value == typeof(MaintenanceItemOutcomeDto) =>
                "Whether the item operation completed successfully.",
            (var value, "residueUncertain") when value == typeof(MaintenanceItemOutcomeDto) =>
                "Whether remaining state requires recovery verification before retrying.",
            (var value, "failureCode") when value == typeof(MaintenanceItemOutcomeDto) =>
                "Stable safe item failure code, when present.",
            (var value, "recoveryGuidance") when value == typeof(MaintenanceItemOutcomeDto) =>
                "Safe recovery guidance without raw paths, SQL, or exception text.",
            (var value, "destructiveAttempted") when value == typeof(MaintenanceOperationOutcomeDto) =>
                "Whether the maintenance workflow crossed a destructive typed seam.",
            (var value, "recoveryRequired") when value == typeof(MaintenanceOperationOutcomeDto) =>
                "Whether the Agent cannot prove cleanup or reset recovery is complete.",
            (var value, "items") when value == typeof(MaintenanceOperationOutcomeDto) =>
                "Sanitized logical item outcomes retained for operator review.",
            (var value, "warnings") when value == typeof(MaintenanceOperationOutcomeDto) =>
                "Bounded safe maintenance warnings.",
            (var value, "recoveryGuidance") when value == typeof(MaintenanceOperationOutcomeDto) =>
                "Safe recovery guidance for partial or ambiguous maintenance outcomes.",
            (var value, "operationId") when value == typeof(MaintenanceOperationDto) =>
                "Opaque maintenance operation handle scoped to the authenticated Windows principal.",
            (var value, "mode") when value == typeof(MaintenanceOperationDto) =>
                "Server-owned maintenance mode: cleanup or branch-reset.",
            (var value, "state") when value == typeof(MaintenanceOperationDto) =>
                "Current maintenance REST/SSE lifecycle state.",
            (var value, "outcome") when value == typeof(MaintenanceOperationDto) =>
                "Maintenance outcome truth; OutcomeUnknown requires recovery inspection before retry.",
            (var value, "progressPercent") when value == typeof(MaintenanceOperationDto) =>
                "Bounded server-reported maintenance progress percentage.",
            (var value, "stage") when value == typeof(MaintenanceOperationDto) =>
                "Server-owned safe maintenance workflow stage.",
            (var value, "detail") when value == typeof(MaintenanceOperationDto) =>
                "Safe operator detail without paths, service names, SQL, credentials, or exceptions.",
            (var value, "startedAtUtc") when value == typeof(MaintenanceOperationDto) =>
                "UTC operation start time recorded by the Agent.",
            (var value, "completedAtUtc") when value == typeof(MaintenanceOperationDto) =>
                "UTC completion time when maintenance reached a final state.",
            (var value, "maintenanceOutcome") when value == typeof(MaintenanceOperationDto) =>
                "Sanitized logical item outcomes and recovery truth, when available.",
            (var value, "errorCode") when value == typeof(MaintenanceOperationDto) =>
                "Stable safe maintenance error code, when the operation did not complete successfully.",
            (var value, "correlationId") when value == typeof(MaintenanceOperationDto) =>
                "Safe request correlation identifier for diagnostics.",
            (var value, "sqlInstance") when value == typeof(RedactedConfigurationDto) =>
                "Configured SQL endpoint label. It is not a credential and does not expose a password.",
            (var value, "sqlUser") when value == typeof(RedactedConfigurationDto) =>
                "Configured SQL user name; the corresponding password is never returned.",
            (var value, "hasSqlPassword") when value == typeof(RedactedConfigurationDto) =>
                "Whether the Agent has a stored SQL password, without returning the password value.",
            (var value, "branchCode") when value == typeof(RedactedConfigurationDto) =>
                "Configured branch code for the local POS device.",
            (var value, "posNumber") when value == typeof(RedactedConfigurationDto) =>
                "Configured POS number for the local device.",
            (var value, "release") when value == typeof(RedactedConfigurationDto) =>
                "Configured RMS+ release label for the local device.",
            (var value, "clientName") when value == typeof(RedactedConfigurationDto) =>
                "Configured client/product label for the local device.",
            (var value, "apiBaseUrl") when value == typeof(RedactedConfigurationDto) =>
                "Configured main API base URL used by the Agent's server-side connectivity check.",
            (var value, "databases") when value == typeof(RedactedConfigurationDto) =>
                "Configured database labels; connection credentials are not included.",
            (var value, "services") when value == typeof(RedactedConfigurationDto) =>
                "Configured Windows service names used internally by the Agent; no control operation is implied.",
            (var value, "downloader") when value == typeof(RedactedConfigurationDto) =>
                "Redacted DB Downloader settings with secret-presence metadata only.",
            (var value, "version") when value == typeof(RedactedConfigurationDto) =>
                "Service-owned optimistic-concurrency version for future configuration mutations.",
            (var value, "apiUrl") when value == typeof(RedactedDownloaderConfigurationDto) =>
                "Configured DB Downloader API URL.",
            (var value, "rdbServerIp") when value == typeof(RedactedDownloaderConfigurationDto) =>
                "Configured RDB server address; no RDB password is returned.",
            (var value, "rdbUsername") when value == typeof(RedactedDownloaderConfigurationDto) =>
                "Configured RDB user name; the corresponding password is never returned.",
            (var value, "hasRdbPassword") when value == typeof(RedactedDownloaderConfigurationDto) =>
                "Whether the Agent has a stored RDB password, without returning the password value.",
            (var value, "knownBranchCodes") when value == typeof(RedactedDownloaderConfigurationDto) =>
                "Configured downloader branch-code labels.",
            (var value, "pollIntervalSeconds") when value == typeof(RedactedDownloaderConfigurationDto) =>
                "Configured downloader polling interval in seconds.",
            (var value, "timeoutSeconds") when value == typeof(RedactedDownloaderConfigurationDto) =>
                "Configured downloader operation timeout in seconds.",
            (var value, "serviceId") when value == typeof(ServiceSummaryDto) =>
                "Opaque server-issued service identifier; the raw Windows service name is not accepted from a browser.",
            (var value, "displayName") when value == typeof(ServiceSummaryDto) =>
                "Safe display name for the allow-listed Windows service.",
            (var value, "installed") when value == typeof(ServiceSummaryDto) =>
                "Whether SCM found the canonical RMS service on this device.",
            (var value, "state") when value == typeof(ServiceSummaryDto) =>
                "Current Windows service runtime state observed by the Agent.",
            (var value, "lastChecked") when value == typeof(ServiceSummaryDto) =>
                "Freshness and check-time evidence for the service state.",
            (var value, "allowedActions") when value == typeof(ServiceSummaryDto) =>
                "Typed service actions currently valid for the observed state. The list is server-owned " +
                "and may be empty when the state is unknown or no action is valid.",
            (var value, "lastOutcome") when value == typeof(ServiceSummaryDto) =>
                "Safe prior-operation outcome, when present; it never contains exception text or target " +
                "details.",
            (var value, "action") when value == typeof(ServiceActionRequestDto) =>
                "One of the explicit Start, Stop, or Restart operations supported by the Agent.",
            (var value, "idempotencyKey") when value == typeof(ServiceActionRequestDto) =>
                "Bounded caller-generated key scoped to the opaque service identifier; repeating the same " +
                "key and action returns the original typed response without a second dispatch.",
            (var value, "outcome") when value == typeof(ServiceActionResponseDto) =>
                "Typed outcome truth: NotAttempted, Failed, Accepted, or OutcomeUnknown.",
            (var value, "code") when value == typeof(ServiceActionResponseDto) =>
                "Stable safe service-action code for operator guidance; it never carries raw exception or " +
                "machine detail.",
            (var value, "detail") when value == typeof(ServiceActionResponseDto) =>
                "Safe operator-facing detail without credentials, Windows SIDs, paths, commands, raw " +
                "service names, or exception text.",
            (var value, "correlationId") when value == typeof(ServiceActionResponseDto) =>
                "Safe Agent/request correlation identifier for diagnostics.",
            (var value, "installed") when value == typeof(RmsInstallationDto) =>
                "Whether known RMS installation metadata or component files were detected.",
            (var value, "branchInstalled") when value == typeof(RmsInstallationDto) =>
                "Whether the known Branch component was detected.",
            (var value, "cashierInstalled") when value == typeof(RmsInstallationDto) =>
                "Whether the known Cashier component was detected.",
            (var value, "branchCode") when value == typeof(RmsInstallationDto) =>
                "Branch code selected from installed RMS metadata.",
            (var value, "posNumber") when value == typeof(RmsInstallationDto) =>
                "POS number selected from installed RMS metadata.",
            (var value, "installationGuid") when value == typeof(RmsInstallationDto) =>
                "Safe installation identifier selected from installed RMS metadata.",
            (var value, "mainServerBranchId") when value == typeof(RmsInstallationDto) =>
                "Main-server branch identifier selected from installed RMS metadata.",
            (var value, "mainServerPosId") when value == typeof(RmsInstallationDto) =>
                "Main-server POS identifier selected from installed RMS metadata.",
            (var value, "mainServerUrl") when value == typeof(RmsInstallationDto) =>
                "Sanitized main-server host and port selected from installed RMS metadata.",
            (var value, "branchServerAddress") when value == typeof(RmsInstallationDto) =>
                "Sanitized Branch-server address selected from installed RMS metadata.",
            (var value, "installationMode") when value == typeof(RmsInstallationDto) =>
                "Detected Branch/Cashier component mode.",
            (var value, "clientName") when value == typeof(RmsInstallationDto) =>
                "Safe client label selected from installed Cashier UI metadata.",
            (var value, "versions") when value == typeof(RmsInstallationDto) =>
                "Build metadata for the installed RMS components.",
            (var value, "consistency") when value == typeof(RmsInstallationDto) =>
                "Cross-file consistency evidence for duplicated RMS values.",
            (var value, "branchServerBuildNumber") when value == typeof(RmsVersionDto) =>
                "Branch Server BuildNumber selected from its installed appsettings.",
            (var value, "cashierServerBuildNumber") when value == typeof(RmsVersionDto) =>
                "Cashier Server BuildNumber selected from its installed appsettings.",
            (var value, "cashierUiBuildNumber") when value == typeof(RmsVersionDto) =>
                "Cashier UI BuildNumber selected from its installed appsettings.",
            (var value, "branchCode") when value == typeof(RmsConsistencyDto) =>
                "Comparison status for duplicated BranchCode values.",
            (var value, "posIdentity") when value == typeof(RmsConsistencyDto) =>
                "Comparison status for duplicated POS identity values.",
            (var value, "mainServerBranchId") when value == typeof(RmsConsistencyDto) =>
                "Comparison status for duplicated main-server branch identifiers.",
            (var value, "mainServerPosId") when value == typeof(RmsConsistencyDto) =>
                "Comparison status for duplicated main-server POS identifiers.",
            (var value, "version") when value == typeof(RmsConsistencyDto) =>
                "Comparison status for installed component build numbers.",
            (var value, "warnings") when value == typeof(RmsConsistencyDto) =>
                "Safe operator warnings for mismatched or unavailable installed metadata.",
            (var value, "configured") when value == typeof(RmsEndpointDiagnosticDto) =>
                "Whether a valid known endpoint was configured.",
            (var value, "endpoint") when value == typeof(RmsEndpointDiagnosticDto) =>
                "Sanitized endpoint host and port; credentials and paths are omitted.",
            (var value, "reachability") when value == typeof(RmsEndpointDiagnosticDto) =>
                "TCP reachability evidence only; application health is not inferred.",
            (var value, "mainServer") when value == typeof(RmsConnectivityDto) =>
                "Main-server configuration and reachability evidence.",
            (var value, "branchServer") when value == typeof(RmsConnectivityDto) =>
                "Branch-server configuration and reachability evidence.",
            (var value, "expectedDatabase") when value == typeof(RmsDatabaseDiagnosticDto) =>
                "Canonical database name expected for this RMS component.",
            (var value, "configuredDatabase") when value == typeof(RmsDatabaseDiagnosticDto) =>
                "Database name parsed from the installed RMS connection string, without returning the string itself.",
            (var value, "serverDisplay") when value == typeof(RmsDatabaseDiagnosticDto) =>
                "Safe SQL data-source label parsed from the installed RMS connection string.",
            (var value, "configured") when value == typeof(RmsDatabaseDiagnosticDto) =>
                "Whether the known RMS connection-string setting is present.",
            (var value, "databaseNameMatches") when value == typeof(RmsDatabaseDiagnosticDto) =>
                "Whether the configured/queried database identity matches the canonical expected database.",
            (var value, "connectivityStatus") when value == typeof(RmsDatabaseDiagnosticDto) =>
                "Sanitized result of configuration validation and the fixed read-only SQL probe.",
            (var value, "evidence") when value == typeof(RmsDatabaseDiagnosticDto) =>
                "Freshness and safe detail for the database diagnostic.",
            (var value, "artifactId") when value == typeof(RmsDatabaseArtifactDto) =>
                "Opaque Agent-owned backup handle; it is not a filesystem path or download URL.",
            (var value, "displayName") when value == typeof(RmsDatabaseArtifactDto) =>
                "Server-generated safe backup display name without a physical path.",
            (var value, "sizeBytes") when value == typeof(RmsDatabaseArtifactDto) =>
                "Size of the approved backup artifact in bytes, when safely known.",
            (var value, "sha256Checksum") when value == typeof(RmsDatabaseArtifactDto) =>
                "Checksum of the approved artifact used for server-side integrity validation.",
            (var value, "createdAtUtc") when value == typeof(RmsDatabaseArtifactDto) =>
                "UTC creation time recorded by the Agent.",
            (var value, "expiresAtUtc") when value == typeof(RmsDatabaseArtifactDto) =>
                "UTC retention expiry, when the Agent assigns one.",
            (var value, "idempotencyKey") when value == typeof(RmsDatabaseBackupRequestDto) =>
                "Bounded caller-generated key scoped to the canonical database and backup operation.",
            (var value, "backupArtifactId") when value == typeof(RmsDatabaseRestoreRequestDto) =>
                "Opaque approved backup handle returned by the Agent workspace; it is not a path.",
            (var value, "confirmationText") when value == typeof(RmsDatabaseRestoreRequestDto) =>
                "Exact target-specific destructive confirmation, for example RESTORE BRANCH DATABASE.",
            (var value, "idempotencyKey") when value == typeof(RmsDatabaseRestoreRequestDto) =>
                "Bounded caller-generated key scoped to the canonical database and restore operation.",
            (var value, "operationId") when value == typeof(RmsDatabaseOperationDto) =>
                "Opaque Agent operation handle scoped to the authenticated Windows principal.",
            (var value, "target") when value == typeof(RmsDatabaseOperationDto) =>
                "Server-owned Branch or Cashier database target.",
            (var value, "databaseDisplayName") when value == typeof(RmsDatabaseOperationDto) =>
                "Safe operator-facing database label.",
            (var value, "operation") when value == typeof(RmsDatabaseOperationDto) =>
                "Typed Backup or Restore operation selected by the server route.",
            (var value, "state") when value == typeof(RmsDatabaseOperationDto) =>
                "Current REST/SSE lifecycle state.",
            (var value, "outcome") when value == typeof(RmsDatabaseOperationDto) =>
                "Typed operation outcome truth; OutcomeUnknown requires inspection before retry.",
            (var value, "progressPercent") when value == typeof(RmsDatabaseOperationDto) =>
                "Bounded server-reported progress percentage.",
            (var value, "stage") when value == typeof(RmsDatabaseOperationDto) =>
                "Server-owned safe workflow stage label.",
            (var value, "detail") when value == typeof(RmsDatabaseOperationDto) =>
                "Safe operator detail without credentials, connection strings, SQL, paths, raw service targets, or exception text.",
            (var value, "startedAtUtc") when value == typeof(RmsDatabaseOperationDto) =>
                "UTC operation start time recorded by the Agent.",
            (var value, "completedAtUtc") when value == typeof(RmsDatabaseOperationDto) =>
                "UTC completion time when the operation reached a final state.",
            (var value, "artifact") when value == typeof(RmsDatabaseOperationDto) =>
                "Sanitized approved artifact metadata, when the workflow produced or used one.",
            (var value, "destructiveAttempted") when value == typeof(RmsDatabaseOperationDto) =>
                "Whether destructive database work was attempted.",
            (var value, "recoveryRequired") when value == typeof(RmsDatabaseOperationDto) =>
                "Whether the Agent cannot prove database/service recovery is complete.",
            (var value, "warnings") when value == typeof(RmsDatabaseOperationDto) =>
                "Bounded safe recovery or operator warnings.",
            (var value, "errorCode") when value == typeof(RmsDatabaseOperationDto) =>
                "Stable safe error code, when the operation did not complete successfully.",
            (var value, "correlationId") when value == typeof(RmsDatabaseOperationDto) =>
                "Safe request correlation identifier for diagnostics.",
            (var value, "target") when value == typeof(RmsDatabaseWorkspaceDto) =>
                "Server-owned Branch or Cashier database target.",
            (var value, "databaseDisplayName") when value == typeof(RmsDatabaseWorkspaceDto) =>
                "Safe operator-facing database label.",
            (var value, "restoreConfirmationText") when value == typeof(RmsDatabaseWorkspaceDto) =>
                "Exact target-specific confirmation text required for destructive restore.",
            (var value, "approvedBackups") when value == typeof(RmsDatabaseWorkspaceDto) =>
                "Approved Agent-owned backup metadata for this canonical target.",
            (var value, "latestOperation") when value == typeof(RmsDatabaseWorkspaceDto) =>
                "Latest principal-scoped operation state, when retained.",
            (var value, "services") when value == typeof(RmsDiagnosticsDto) =>
                "Current SCM status for the bounded canonical RMS service catalog.",
            (var value, "installation") when value == typeof(RmsDiagnosticsDto) =>
                "Safe installed RMS identity and consistency evidence.",
            (var value, "connectivity") when value == typeof(RmsDiagnosticsDto) =>
                "Safe main-server and Branch-server reachability evidence.",
            (var value, "branchDatabase") when value == typeof(RmsDiagnosticsDto) =>
                "Sanitized Branch database diagnostic for RmsBranchSrv.",
            (var value, "cashierDatabase") when value == typeof(RmsDiagnosticsDto) =>
                "Sanitized Cashier database diagnostic for RmsCashierSrv.",
            _ => null
        };
}
