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
            "server-owned RMS database backup/restore workflows. Mutation tokens are short-lived, " +
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
        AddWindowsSecurityRequirement(document, "/api/v1/rms/databases/{targetId}", HttpMethod.Get);
        AddWindowsSecurityRequirement(document, "/api/v1/rms/databases/{targetId}/backup", HttpMethod.Post);
        AddWindowsSecurityRequirement(document, "/api/v1/rms/databases/{targetId}/restore", HttpMethod.Post);
        AddWindowsSecurityRequirement(document, "/api/v1/rms/databases/{targetId}/operations/{operationId}", HttpMethod.Get);
        AddWindowsSecurityRequirement(document, "/api/v1/rms/databases/{targetId}/operations/{operationId}/events", HttpMethod.Get);
        AddReferencePropertyDescriptions(document);

        return Task.CompletedTask;
    }

    private static void AddReferencePropertyDescriptions(OpenApiDocument document)
    {
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
