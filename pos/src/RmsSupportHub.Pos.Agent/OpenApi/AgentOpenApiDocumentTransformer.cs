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
            "Administrators membership authorizes protected reads and the typed Windows-service " +
            "control operation, and no Support Hub backend relay is involved. INT-08 exposes read-only " +
            "device/configuration/service visibility plus one allow-listed Start/Stop/Restart service " +
            "mutation. Mutation tokens are short-lived, one-use, and bound to the authenticated " +
            "principal, exact Origin, registered operation, target, method, and path.";
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
