using System.Net;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using RmsSupportHub.Pos.Agent;

namespace RmsSupportHub.Pos.Agent.IntegrationTests;

public sealed class OpenApiContractTests : IClassFixture<AgentWebApplicationFactory>
{
    private readonly AgentWebApplicationFactory _factory;

    public OpenApiContractTests(AgentWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task IntegrationDocumentContainsOnlyTheCurrentFoundationSurface()
    {
        using var client = _factory.CreateSecureClient();
        var document = await GetDocumentAsync(client);
        var paths = document["paths"]!.AsObject();

        Assert.Equal(
            [
                "/api/v1/security/mutation-token",
                "/api/v1/session",
                "/health/live",
                "/health/ready"
            ],
            paths.Select(entry => entry.Key).OrderBy(path => path, StringComparer.Ordinal));

        foreach (var forbidden in new[] { "backup", "restore", "maintenance", "downloader", "configuration", "service" })
        {
            Assert.DoesNotContain(
                paths.Select(entry => entry.Key),
                path => path.Contains(forbidden, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task DocumentUsesStableOperationsCanonicalServerAndWindowsNegotiate()
    {
        using var client = _factory.CreateSecureClient();
        var document = await GetDocumentAsync(client);

        Assert.Equal("GetHealthLive", Operation(document, "/health/live", "get")["operationId"]!.GetValue<string>());
        Assert.Equal("GetHealthReady", Operation(document, "/health/ready", "get")["operationId"]!.GetValue<string>());
        Assert.Equal("GetAgentSession", Operation(document, "/api/v1/session", "get")["operationId"]!.GetValue<string>());
        Assert.Equal("IssueMutationToken", Operation(document, "/api/v1/security/mutation-token", "post")["operationId"]!.GetValue<string>());

        var servers = document["servers"]!.AsArray();
        Assert.Single(servers);
        Assert.Equal(AgentHostConstants.CanonicalOrigin, servers[0]!["url"]!.GetValue<string>());

        var scheme = document["components"]!["securitySchemes"]!["WindowsNegotiate"]!.AsObject();
        Assert.Equal("http", scheme["type"]!.GetValue<string>());
        Assert.Equal("negotiate", scheme["scheme"]!.GetValue<string>());
        Assert.False(scheme.ContainsKey("bearerFormat"));

        Assert.False(Operation(document, "/health/live", "get").ContainsKey("security"));
        Assert.False(Operation(document, "/health/ready", "get").ContainsKey("security"));
        Assert.True(Operation(document, "/api/v1/session", "get").ContainsKey("security"));
        Assert.True(Operation(document, "/api/v1/security/mutation-token", "post").ContainsKey("security"));

        var serialized = document.ToJsonString();
        Assert.DoesNotContain("bearer", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("jwt", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("oot_sid", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"Authorization\":", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EveryAgentOperationHasCompleteDocumentationMetadata()
    {
        using var client = _factory.CreateSecureClient();
        var document = await GetDocumentAsync(client);
        var paths = document["paths"]!.AsObject();

        foreach (var path in paths)
        {
            foreach (var operationEntry in path.Value!.AsObject())
            {
                if (operationEntry.Key is not ("get" or "post" or "put" or "patch" or "delete"))
                {
                    continue;
                }

                var operation = operationEntry.Value!.AsObject();
                Assert.False(string.IsNullOrWhiteSpace(operation["operationId"]?.GetValue<string>()),
                    $"{operationEntry.Key.ToUpperInvariant()} {path.Key} has no operationId.");
                Assert.False(string.IsNullOrWhiteSpace(operation["summary"]?.GetValue<string>()),
                    $"{operationEntry.Key.ToUpperInvariant()} {path.Key} has no summary.");
                Assert.False(string.IsNullOrWhiteSpace(operation["description"]?.GetValue<string>()),
                    $"{operationEntry.Key.ToUpperInvariant()} {path.Key} has no description.");

                var tags = operation["tags"]?.AsArray();
                Assert.NotNull(tags);
                Assert.NotEmpty(tags!);
                Assert.All(tags!, tag => Assert.False(string.IsNullOrWhiteSpace(tag!.GetValue<string>())));

                var responses = operation["responses"]?.AsObject();
                Assert.NotNull(responses);
                Assert.NotEmpty(responses!);
                Assert.All(
                    responses!,
                    response => Assert.True(
                        (response.Value?["description"]?.GetValue<string>()?.Length ?? 0) >= 20,
                        $"{operationEntry.Key.ToUpperInvariant()} {path.Key} response {response.Key} lacks a semantic description."));

            }
        }

        var mutation = Operation(document, "/api/v1/security/mutation-token", "post");
        Assert.Contains("operationId", mutation["requestBody"]!["description"]!.GetValue<string>());
        Assert.Contains("local Built-in Administrators", mutation["description"]!.GetValue<string>());
        Assert.NotNull(mutation["security"]);

        var session = Operation(document, "/api/v1/session", "get");
        Assert.NotNull(session["security"]);
        Assert.Null(Operation(document, "/health/live", "get")["security"]);
        Assert.Null(Operation(document, "/health/ready", "get")["security"]);
    }

    [Fact]
    public async Task DocumentedResponseContractsMatchTheRuntimePaths()
    {
        using var client = _factory.CreateSecureClient();
        var document = await GetDocumentAsync(client);

        var live = Operation(document, "/health/live", "get");
        AssertResponseSchema(live, "200", "application/json", "HealthStatusDto");
        Assert.Equal("live", ResponseExample(live, "200", "application/json")["status"]!.GetValue<string>());

        var ready = Operation(document, "/health/ready", "get");
        AssertResponseSchema(ready, "200", "application/json", "HealthStatusDto");
        Assert.Equal("ready", ResponseExample(ready, "200", "application/json")["status"]!.GetValue<string>());

        var session = Operation(document, "/api/v1/session", "get");
        AssertResponseSchema(session, "200", "application/json", "SessionInfoDto");
        Assert.Equal(
            "EXAMPLE\\support-user",
            ResponseExample(session, "200", "application/json")["principalName"]!.GetValue<string>());
        AssertBodylessFrameworkResponse(session, "401");
        AssertNegotiateChallenge(session, "401");
        AssertResponseSchema(session, "403", "application/problem+json", "AgentProblemDetailsDto");
        Assert.Contains(
            "windows_sid_unavailable",
            session["responses"]!["403"]!["description"]!.GetValue<string>(),
            StringComparison.Ordinal);

        var mutation = Operation(document, "/api/v1/security/mutation-token", "post");
        AssertResponseSchema(mutation, "200", "application/json", "MutationTokenIssueResponseDto");
        Assert.Equal(
            "opaque-placeholder-not-a-real-token",
            ResponseExample(mutation, "200", "application/json")["token"]!.GetValue<string>());
        Assert.Equal(
            "example.registered-operation",
            mutation["requestBody"]!["content"]!["application/json"]!["example"]!["operationId"]!.GetValue<string>());
        AssertResponseSchema(mutation, "400", "application/problem+json", "AgentProblemDetailsDto");
        Assert.Contains(
            "operation_not_supported",
            mutation["responses"]!["400"]!["description"]!.GetValue<string>(),
            StringComparison.Ordinal);
        AssertBodylessFrameworkResponse(mutation, "401");
        AssertNegotiateChallenge(mutation, "401");
        var forbidden = mutation["responses"]!["403"]!.AsObject();
        Assert.False(forbidden.ContainsKey("content"));
        var forbiddenDescription = forbidden["description"]!.GetValue<string>();
        Assert.Contains("AuthorizationMiddleware", forbiddenDescription, StringComparison.Ordinal);
        Assert.Contains("bodyless", forbiddenDescription, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("windows_sid_unavailable", forbiddenDescription, StringComparison.Ordinal);
        AssertResponseSchema(mutation, "429", "application/problem+json", "AgentProblemDetailsDto");
        Assert.Contains(
            "mutation_token_capacity",
            mutation["responses"]!["429"]!["description"]!.GetValue<string>(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task EveryAgentHttpOperationHasMatchingOpenApiOperation()
    {
        using var client = _factory.CreateSecureClient();
        var document = await GetDocumentAsync(client);

        using var scope = _factory.Services.CreateScope();
        var actual = scope.ServiceProvider
            .GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .SelectMany(endpoint =>
            {
                var path = NormalizePath(endpoint.RoutePattern.RawText);
                var methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? [];
                return methods.Select(method => (Path: path, Method: method.ToUpperInvariant()));
            })
            .Where(operation => !IsDocumentationRoute(operation.Path))
            .Select(operation => $"{operation.Method} {operation.Path}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var documented = document["paths"]!
            .AsObject()
            .SelectMany(path => path.Value!.AsObject()
                .Where(operation => operation.Key is "get" or "post" or "put" or "patch" or "delete")
                .Select(operation => $"{operation.Key.ToUpperInvariant()} {path.Key}"))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingDocumentation = actual.Except(documented, StringComparer.OrdinalIgnoreCase).OrderBy(value => value).ToArray();
        var undocumentedRuntimeOperations = documented.Except(actual, StringComparer.OrdinalIgnoreCase).OrderBy(value => value).ToArray();

        Assert.True(
            actual.SetEquals(documented),
            $"Agent/OpenAPI operation parity failed. Missing documentation: [{string.Join(", ", missingDocumentation)}]. " +
            $"Documented but not mapped: [{string.Join(", ", undocumentedRuntimeOperations)}].");
    }

    [Fact]
    public async Task ReachableSchemasHaveDescriptionsForEveryExposedProperty()
    {
        using var client = _factory.CreateSecureClient();
        var document = await GetDocumentAsync(client);
        var schemas = document["components"]!["schemas"]!.AsObject();

        foreach (var schemaName in new[]
        {
            "HealthStatusDto",
            "SessionInfoDto",
            "MutationTokenIssueRequestDto",
            "MutationTokenIssueResponseDto",
            "AgentProblemDetailsDto"
        })
        {
            var schema = schemas[schemaName]!.AsObject();
            Assert.False(string.IsNullOrWhiteSpace(schema["description"]?.GetValue<string>()), schemaName);
            var properties = schema["properties"]!.AsObject();
            Assert.NotEmpty(properties);
            Assert.All(
                properties,
                property => Assert.False(
                    string.IsNullOrWhiteSpace(property.Value?["description"]?.GetValue<string>()),
                    $"{schemaName}.{property.Key} has no description."));
        }
    }

    [Fact]
    public async Task ScalarAndOpenApiAreReachableInIntegrationTest()
    {
        using var client = _factory.CreateSecureClient();

        var scalarRedirect = await client.GetAsync("/scalar");
        Assert.Equal(HttpStatusCode.Found, scalarRedirect.StatusCode);
        Assert.Equal("scalar/", scalarRedirect.Headers.Location?.OriginalString);

        var scalarResponse = await client.GetAsync("/scalar/");
        var scalarHtml = await scalarResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, scalarResponse.StatusCode);
        Assert.Contains("RMS+ POS Agent API", scalarHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("cdn.jsdelivr.net", scalarHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fonts.googleapis.com", scalarHtml, StringComparison.OrdinalIgnoreCase);

        var openApiResponse = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, openApiResponse.StatusCode);
    }

    [Fact]
    public async Task PublicTokenSchemasExposeOnlyOperationIdAndOpaqueResponseFields()
    {
        using var client = _factory.CreateSecureClient();
        var document = await GetDocumentAsync(client);
        var schemas = document["components"]!["schemas"]!.AsObject();

        var requestSchema = schemas["MutationTokenIssueRequestDto"]!.AsObject();
        Assert.Equal(["operationId"], PropertyNames(requestSchema));
        Assert.Equal(["operationId"], RequiredNames(requestSchema));

        var responseSchema = schemas["MutationTokenIssueResponseDto"]!.AsObject();
        Assert.Equal(["token", "expiresAtUtc"], PropertyNames(responseSchema));
        Assert.Equal(["token", "expiresAtUtc"], RequiredNames(responseSchema));

        var problemSchema = schemas["AgentProblemDetailsDto"]!.AsObject();
        var problemFields = PropertyNames(problemSchema);
        Assert.DoesNotContain(problemFields, field => field.Contains("sid", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(problemFields, field => field.Contains("path", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(problemFields, field => field.Contains("target", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(problemFields, field => field.Contains("secret", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ProductionDoesNotExposeRuntimeOpenApi()
    {
        using var factory = new AgentWebApplicationFactory("Production");
        using var scope = factory.Services.CreateScope();
        var endpoints = scope.ServiceProvider
            .GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .Where(path => path is not null);

        Assert.DoesNotContain(endpoints, path => path!.Contains("/openapi/", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(endpoints, path => path!.Equals("/scalar", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<JsonObject> GetDocumentAsync(HttpClient client)
    {
        var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var document = await JsonNode.ParseAsync(await response.Content.ReadAsStreamAsync());
        return document?.AsObject() ?? throw new InvalidOperationException("OpenAPI document was empty.");
    }

    private static JsonObject Operation(JsonObject document, string path, string method) =>
        document["paths"]![path]![method]!.AsObject();

    private static void AssertResponseSchema(
        JsonObject operation,
        string statusCode,
        string mediaType,
        string schemaName)
    {
        var schemaReference = operation["responses"]![statusCode]!["content"]![mediaType]!["schema"]!["$ref"]!
            .GetValue<string>();
        Assert.Equal($"#/components/schemas/{schemaName}", schemaReference);
    }

    private static void AssertBodylessFrameworkResponse(JsonObject operation, string statusCode)
    {
        var response = operation["responses"]![statusCode]!.AsObject();
        Assert.False(response.ContainsKey("content"));
    }

    private static void AssertNegotiateChallenge(JsonObject operation, string statusCode)
    {
        var response = operation["responses"]![statusCode]!.AsObject();
        var description = response["description"]!.GetValue<string>();
        var header = response["headers"]!["WWW-Authenticate"]!.AsObject();
        Assert.Contains("Negotiate", description, StringComparison.Ordinal);
        Assert.Contains("Negotiate", header["description"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal("string", header["schema"]!["type"]!.GetValue<string>());
        Assert.Equal("Negotiate", header["schema"]!["example"]!.GetValue<string>());
    }

    private static JsonObject ResponseExample(JsonObject operation, string statusCode, string mediaType) =>
        operation["responses"]![statusCode]!["content"]![mediaType]!["example"]!.AsObject();

    private static string NormalizePath(string? rawPath) =>
        "/" + (rawPath ?? string.Empty).TrimStart('/');

    private static bool IsDocumentationRoute(string path) =>
        path.Equals("/scalar", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/scalar/", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/openapi/", StringComparison.OrdinalIgnoreCase);

    private static string[] PropertyNames(JsonObject schema) =>
        schema["properties"]!.AsObject().Select(property => property.Key).ToArray();

    private static string[] RequiredNames(JsonObject schema) =>
        schema["required"]!.AsArray().Select(value => value!.GetValue<string>()).ToArray();
}
