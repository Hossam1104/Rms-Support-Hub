using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using RmsSupportHub.Pos.Contracts.V1.Common;
using RmsSupportHub.Pos.Contracts.V1.Configuration;
using RmsSupportHub.Pos.Contracts.V1.Device;
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
            (var value, null) when value == typeof(ServiceActionOutcome) =>
                "Typed service-action outcome: NotAttempted, Failed, Accepted, or OutcomeUnknown.",
            (var value, null) when value == typeof(EvidenceDto) =>
                "A bounded status observation with explicit freshness and UTC check time.",
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
