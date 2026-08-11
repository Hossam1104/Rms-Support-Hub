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
            "Destination-owned foundation contract for the per-device POS Agent. " +
            "Browser credentials are supplied by Windows Negotiate; mutation tokens are " +
            "short-lived, one-use, and server-operation-bound.";
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

        return Task.CompletedTask;
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
