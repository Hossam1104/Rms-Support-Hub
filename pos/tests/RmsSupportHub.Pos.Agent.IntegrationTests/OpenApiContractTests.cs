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
    public async Task IntegrationDocumentContainsTheInt08ServiceControlSurface()
    {
        using var client = _factory.CreateSecureClient();
        var document = await GetDocumentAsync(client);
        var paths = document["paths"]!.AsObject();

        Assert.Equal(
            [
                "/api/v1/artifacts/{artifactId}",
                "/api/v1/configuration",
                "/api/v1/device/capabilities",
                "/api/v1/device/connectivity",
                "/api/v1/device/identity",
                "/api/v1/diagnostic-console/preview/{targetId}",
                "/api/v1/diagnostic-console/runs",
                "/api/v1/diagnostic-console/runs/{operationId}",
                "/api/v1/diagnostics/services/{serviceId}/failure",
                "/api/v1/diagnostics/timeline",
                "/api/v1/downloads/batches",
                "/api/v1/downloads/branches",
                "/api/v1/downloads/operations/{operationId}",
                "/api/v1/downloads/operations/{operationId}/events",
                "/api/v1/health/check",
                "/api/v1/main-server/profiles",
                "/api/v1/main-server/state",
                "/api/v1/maintenance/cleanup/execute",
                "/api/v1/maintenance/cleanup/preview",
                "/api/v1/maintenance/operations/{operationId}",
                "/api/v1/maintenance/operations/{operationId}/events",
                "/api/v1/maintenance/reset/execute",
                "/api/v1/maintenance/reset/preview",
                "/api/v1/packages/operations",
                "/api/v1/packages/operations/{operationId}",
                "/api/v1/packages/preview/{operationId}",
                "/api/v1/packages/status",
                "/api/v1/repair/guided/preview",
                "/api/v1/repair/guided/preview/{snapshotId}",
                "/api/v1/repair/guided/steps",
                "/api/v1/repair/guided/{guidedRepairId}",
                "/api/v1/repair/operations",
                "/api/v1/repair/operations/{operationId}",
                "/api/v1/repair/preview/{operationId}",
                "/api/v1/repair/preview/{operationId}/{snapshotId}",
                "/api/v1/rms/databases/{targetId}",
                "/api/v1/rms/databases/{targetId}/backup",
                "/api/v1/rms/databases/{targetId}/operations/{operationId}",
                "/api/v1/rms/databases/{targetId}/operations/{operationId}/events",
                "/api/v1/rms/databases/{targetId}/restore",
                "/api/v1/rms/diagnostics",
                "/api/v1/rms/operational-health",
                "/api/v1/safety-snapshots/capture",
                "/api/v1/safety-snapshots/preview",
                "/api/v1/safety-snapshots/{snapshotId}",
                "/api/v1/safety-snapshots/{snapshotId}/verify",
                "/api/v1/security/mutation-token",
                "/api/v1/services",
                "/api/v1/services/{serviceId}/actions",
                "/api/v1/session",
                "/api/v1/support-bundles",
                "/health/live",
                "/health/ready"
            ],
            paths.Select(entry => entry.Key).OrderBy(path => path, StringComparer.Ordinal));
    }

    [Fact]
    public async Task DocumentUsesStableOperationsCanonicalServerAndWindowsNegotiate()
    {
        using var client = _factory.CreateSecureClient();
        var document = await GetDocumentAsync(client);

        Assert.Equal("GetHealthLive", Operation(document, "/health/live", "get")["operationId"]!.GetValue<string>());
        Assert.Equal("GetHealthReady", Operation(document, "/health/ready", "get")["operationId"]!.GetValue<string>());
        Assert.Equal("GetHealthCheck", Operation(document, "/api/v1/health/check", "get")["operationId"]!.GetValue<string>());
        Assert.Equal("GetAgentSession", Operation(document, "/api/v1/session", "get")["operationId"]!.GetValue<string>());
        Assert.Equal("IssueMutationToken", Operation(document, "/api/v1/security/mutation-token", "post")["operationId"]!.GetValue<string>());
        Assert.Equal("GetDeviceIdentity", Operation(document, "/api/v1/device/identity", "get")["operationId"]!.GetValue<string>());
        Assert.Equal("GetDeviceConnectivity", Operation(document, "/api/v1/device/connectivity", "get")["operationId"]!.GetValue<string>());
        Assert.Equal("GetDeviceCapabilities", Operation(document, "/api/v1/device/capabilities", "get")["operationId"]!.GetValue<string>());
        Assert.Equal("GetConfiguration", Operation(document, "/api/v1/configuration", "get")["operationId"]!.GetValue<string>());
        Assert.Equal("GetServices", Operation(document, "/api/v1/services", "get")["operationId"]!.GetValue<string>());
        Assert.Equal("ControlService", Operation(document, "/api/v1/services/{serviceId}/actions", "post")["operationId"]!.GetValue<string>());
        Assert.Equal("GetRmsDiagnostics", Operation(document, "/api/v1/rms/diagnostics", "get")["operationId"]!.GetValue<string>());
        Assert.Equal("GetServiceFailureAnalysis", Operation(document, "/api/v1/diagnostics/services/{serviceId}/failure", "get")["operationId"]!.GetValue<string>());
        Assert.Equal("GetIncidentTimeline", Operation(document, "/api/v1/diagnostics/timeline", "get")["operationId"]!.GetValue<string>());
        Assert.Equal("GenerateSupportBundle", Operation(document, "/api/v1/support-bundles", "post")["operationId"]!.GetValue<string>());
        Assert.Equal("GetRmsDatabaseWorkspace", Operation(document, "/api/v1/rms/databases/{targetId}", "get")["operationId"]!.GetValue<string>());
        Assert.Equal("BackupRmsDatabase", Operation(document, "/api/v1/rms/databases/{targetId}/backup", "post")["operationId"]!.GetValue<string>());
        Assert.Equal("RestoreRmsDatabase", Operation(document, "/api/v1/rms/databases/{targetId}/restore", "post")["operationId"]!.GetValue<string>());
        Assert.Equal("GetRmsDatabaseOperation", Operation(document, "/api/v1/rms/databases/{targetId}/operations/{operationId}", "get")["operationId"]!.GetValue<string>());
        Assert.Equal("StreamRmsDatabaseOperationEvents", Operation(document, "/api/v1/rms/databases/{targetId}/operations/{operationId}/events", "get")["operationId"]!.GetValue<string>());
        Assert.Equal("GetDownloaderBranches", Operation(document, "/api/v1/downloads/branches", "get")["operationId"]!.GetValue<string>());
        Assert.Equal("TriggerDownloaderBatch", Operation(document, "/api/v1/downloads/batches", "post")["operationId"]!.GetValue<string>());
        Assert.Equal("GetDownloaderOperation", Operation(document, "/api/v1/downloads/operations/{operationId}", "get")["operationId"]!.GetValue<string>());
        Assert.Equal("StreamDownloaderOperationEvents", Operation(document, "/api/v1/downloads/operations/{operationId}/events", "get")["operationId"]!.GetValue<string>());
        Assert.Equal("PreviewMaintenanceCleanup", Operation(document, "/api/v1/maintenance/cleanup/preview", "post")["operationId"]!.GetValue<string>());
        Assert.Equal("ExecuteMaintenanceCleanup", Operation(document, "/api/v1/maintenance/cleanup/execute", "post")["operationId"]!.GetValue<string>());
        Assert.Equal("PreviewMaintenanceBranchReset", Operation(document, "/api/v1/maintenance/reset/preview", "post")["operationId"]!.GetValue<string>());
        Assert.Equal("ExecuteMaintenanceBranchReset", Operation(document, "/api/v1/maintenance/reset/execute", "post")["operationId"]!.GetValue<string>());
        Assert.Equal("GetMaintenanceOperation", Operation(document, "/api/v1/maintenance/operations/{operationId}", "get")["operationId"]!.GetValue<string>());
        Assert.Equal("StreamMaintenanceOperationEvents", Operation(document, "/api/v1/maintenance/operations/{operationId}/events", "get")["operationId"]!.GetValue<string>());
        Assert.Equal("DownloadArtifact", Operation(document, "/api/v1/artifacts/{artifactId}", "get")["operationId"]!.GetValue<string>());
        Assert.Equal("PreviewDiagnosticConsoleRun", Operation(document, "/api/v1/diagnostic-console/preview/{targetId}", "post")["operationId"]!.GetValue<string>());
        Assert.Equal("PreviewAgentPackageOperation", Operation(document, "/api/v1/packages/preview/{operationId}", "post")["operationId"]!.GetValue<string>());
        Assert.Equal("PreviewRepairWithoutSnapshot", Operation(document, "/api/v1/repair/preview/{operationId}", "post")["operationId"]!.GetValue<string>());
        Assert.Equal("PreviewRepairWithSnapshot", Operation(document, "/api/v1/repair/preview/{operationId}/{snapshotId}", "post")["operationId"]!.GetValue<string>());
        Assert.Equal("PreviewGuidedRepair", Operation(document, "/api/v1/repair/guided/preview", "post")["operationId"]!.GetValue<string>());
        Assert.Equal("PreviewGuidedRepairWithSnapshot", Operation(document, "/api/v1/repair/guided/preview/{snapshotId}", "post")["operationId"]!.GetValue<string>());

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
        Assert.True(Operation(document, "/api/v1/device/identity", "get").ContainsKey("security"));
        Assert.True(Operation(document, "/api/v1/device/connectivity", "get").ContainsKey("security"));
        Assert.True(Operation(document, "/api/v1/device/capabilities", "get").ContainsKey("security"));
        Assert.True(Operation(document, "/api/v1/configuration", "get").ContainsKey("security"));
        Assert.True(Operation(document, "/api/v1/services", "get").ContainsKey("security"));
        Assert.True(Operation(document, "/api/v1/services/{serviceId}/actions", "post").ContainsKey("security"));
        Assert.True(Operation(document, "/api/v1/rms/diagnostics", "get").ContainsKey("security"));
        Assert.True(Operation(document, "/api/v1/health/check", "get").ContainsKey("security"));
        Assert.True(Operation(document, "/api/v1/diagnostics/services/{serviceId}/failure", "get").ContainsKey("security"));
        Assert.True(Operation(document, "/api/v1/diagnostics/timeline", "get").ContainsKey("security"));
        Assert.True(Operation(document, "/api/v1/support-bundles", "post").ContainsKey("security"));
        Assert.True(Operation(document, "/api/v1/rms/databases/{targetId}", "get").ContainsKey("security"));
        Assert.True(Operation(document, "/api/v1/rms/databases/{targetId}/backup", "post").ContainsKey("security"));
        Assert.True(Operation(document, "/api/v1/rms/databases/{targetId}/restore", "post").ContainsKey("security"));
        Assert.True(Operation(document, "/api/v1/rms/databases/{targetId}/operations/{operationId}", "get").ContainsKey("security"));
        Assert.True(Operation(document, "/api/v1/rms/databases/{targetId}/operations/{operationId}/events", "get").ContainsKey("security"));
        Assert.True(Operation(document, "/api/v1/downloads/branches", "get").ContainsKey("security"));
        Assert.True(Operation(document, "/api/v1/downloads/batches", "post").ContainsKey("security"));
        Assert.True(Operation(document, "/api/v1/downloads/operations/{operationId}", "get").ContainsKey("security"));
        Assert.True(Operation(document, "/api/v1/downloads/operations/{operationId}/events", "get").ContainsKey("security"));
        Assert.True(Operation(document, "/api/v1/maintenance/cleanup/preview", "post").ContainsKey("security"));
        Assert.True(Operation(document, "/api/v1/maintenance/cleanup/execute", "post").ContainsKey("security"));
        Assert.True(Operation(document, "/api/v1/maintenance/reset/preview", "post").ContainsKey("security"));
        Assert.True(Operation(document, "/api/v1/maintenance/reset/execute", "post").ContainsKey("security"));
        Assert.True(Operation(document, "/api/v1/maintenance/operations/{operationId}", "get").ContainsKey("security"));
        Assert.True(Operation(document, "/api/v1/maintenance/operations/{operationId}/events", "get").ContainsKey("security"));
        Assert.True(Operation(document, "/api/v1/artifacts/{artifactId}", "get").ContainsKey("security"));

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
        Assert.Contains("targetId", mutation["requestBody"]!["description"]!.GetValue<string>());
        Assert.NotNull(mutation["security"]);

        var serviceAction = Operation(document, "/api/v1/services/{serviceId}/actions", "post");
        Assert.Contains("OutcomeUnknown", serviceAction["description"]!.GetValue<string>());
        Assert.Contains("never retried", serviceAction["description"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(serviceAction["security"]);

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

        var identity = Operation(document, "/api/v1/device/identity", "get");
        AssertResponseSchema(identity, "200", "application/json", "DeviceIdentityDto");
        Assert.Equal("BR-001", ResponseExample(identity, "200", "application/json")["branchCode"]!.GetValue<string>());
        AssertProtectedReadResponses(identity);

        var connectivity = Operation(document, "/api/v1/device/connectivity", "get");
        AssertResponseSchema(connectivity, "200", "application/json", "DeviceConnectivityDto");
        Assert.Equal("fresh", ResponseExample(connectivity, "200", "application/json")["localSql"]!["freshness"]!.GetValue<string>());
        AssertProtectedReadResponses(connectivity);

        var capabilities = Operation(document, "/api/v1/device/capabilities", "get");
        AssertResponseSchema(capabilities, "200", "application/json", "DeviceCapabilitiesDto");
        Assert.Empty(ResponseExample(capabilities, "200", "application/json")["browseRoots"]!.AsArray());
        AssertProtectedReadResponses(capabilities);

        var configuration = Operation(document, "/api/v1/configuration", "get");
        AssertResponseSchema(configuration, "200", "application/json", "RedactedConfigurationDto");
        var configurationExample = ResponseExample(configuration, "200", "application/json");
        Assert.False(configurationExample["hasSqlPassword"]!.GetValue<bool>());
        Assert.False(configurationExample["downloader"]!["hasRdbPassword"]!.GetValue<bool>());
        AssertProtectedReadResponses(configuration);

        var services = Operation(document, "/api/v1/services", "get");
        AssertArrayResponseSchema(services, "200", "application/json", "ServiceSummaryDto");
        Assert.Equal(
            ["stop", "restart"],
            ResponseArrayExample(services, "200", "application/json")[0]!["allowedActions"]!.AsArray()
                .Select(value => value!.GetValue<string>()).ToArray());
        AssertProtectedReadResponses(services);

        var rmsDiagnostics = Operation(document, "/api/v1/rms/diagnostics", "get");
        AssertResponseSchema(rmsDiagnostics, "200", "application/json", "RmsDiagnosticsDto");
        Assert.Equal(
            "RmsBranchSrv",
            ResponseExample(rmsDiagnostics, "200", "application/json")["branchDatabase"]!["expectedDatabase"]!.GetValue<string>());
        AssertProtectedReadResponses(rmsDiagnostics);

        var serviceAction = Operation(document, "/api/v1/services/{serviceId}/actions", "post");
        AssertResponseSchema(serviceAction, "200", "application/json", "ServiceActionResponseDto");
        var actionRequestExample = serviceAction["requestBody"]!["content"]!["application/json"]!["example"]!.AsObject();
        Assert.Equal("restart", actionRequestExample["action"]!.GetValue<string>());
        Assert.False(string.IsNullOrWhiteSpace(actionRequestExample["idempotencyKey"]!.GetValue<string>()));
        var actionResponseExample = ResponseExample(serviceAction, "200", "application/json");
        Assert.Equal("accepted", actionResponseExample["outcome"]!.GetValue<string>());
        Assert.Equal("service_action_accepted", actionResponseExample["code"]!.GetValue<string>());
        AssertResponseSchema(serviceAction, "403", "application/problem+json", "AgentProblemDetailsDto");
        Assert.Contains("mutation_token_invalid", serviceAction["responses"]!["403"]!["description"]!.GetValue<string>(), StringComparison.Ordinal);
        AssertResponseSchema(serviceAction, "500", "application/problem+json", "AgentProblemDetailsDto");
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
            "AgentProblemDetailsDto",
            "DeviceIdentityDto",
            "DeviceConnectivityDto",
            "DeviceCapabilitiesDto",
            "BrowseRootDto",
            "EvidenceDto",
            "RedactedConfigurationDto",
            "RedactedDownloaderConfigurationDto",
            "ServiceSummaryDto",
            "ServiceActionRequestDto",
            "ServiceActionResponseDto",
            "RmsDiagnosticsDto",
            "RmsInstallationDto",
            "RmsVersionDto",
            "RmsConsistencyDto",
            "RmsEndpointDiagnosticDto",
            "RmsConnectivityDto",
            "RmsDatabaseDiagnosticDto"
            ,"RmsDatabaseHealthDto"
            ,"RmsDatabaseBackupHealthDto"
            ,"RmsStorageHealthDto"
            ,"HealthReportDto"
            ,"HealthCheckDto"
            ,"RmsComponentDriftDto"
            ,"ServiceFailureAnalysisDto"
            ,"FailureEvidenceDto"
            ,"FailureRecommendationDto"
            ,"IncidentTimelineDto"
            ,"IncidentTimelineEventDto"
            ,"SupportBundleDto"
            ,"RmsDatabaseArtifactDto"
            ,"RmsDatabaseBackupRequestDto"
            ,"RmsDatabaseRestoreRequestDto"
            ,"RmsDatabaseOperationDto"
            ,"RmsDatabaseWorkspaceDto"
            ,"BranchCatalogEntryDto"
            ,"TriggerBatchRequestDto"
            ,"DownloaderBranchOutcomeDto"
            ,"DownloaderOperationOutcomeDto"
            ,"DownloaderOperationDto"
            ,"CleanupExecuteRequestDto"
            ,"BranchResetExecuteRequestDto"
            ,"CleanupPreviewDto"
            ,"CleanupTargetPreviewDto"
            ,"BranchResetPreviewDto"
            ,"BranchResetTablePreviewDto"
            ,"MaintenancePolicyRejectionDto"
             ,"MaintenanceItemOutcomeDto"
             ,"MaintenanceOperationOutcomeDto"
             ,"MaintenanceOperationDto"
             ,"MainServerProfileDto"
             ,"MainServerProfilesDto"
             ,"MainServerStateEvidenceDto"
             ,"SafetySnapshotCaptureRequestDto"
             ,"SafetySnapshotPreviewDto"
             ,"SafetySnapshotDto"
             ,"SafetySnapshotVerificationDto"
             ,"DiagnosticConsolePreviewDto"
             ,"DiagnosticConsolePreviewRequestDto"
             ,"DiagnosticConsoleStartRequestDto"
             ,"DiagnosticConsoleRunDto"
             ,"DiagnosticConsoleResultDto"
             ,"AgentPackageFileDto"
             ,"AgentPackageManifestDto"
             ,"AgentPackageStatusDto"
             ,"AgentPackagePreviewDto"
             ,"AgentPackagePreviewRequestDto"
             ,"AgentPackageOperationRequestDto"
             ,"AgentPackageOperationDto"
             ,"RepairPreviewDto"
             ,"RepairPreviewRequestDto"
             ,"GuidedRepairPreviewRequestDto"
             ,"RepairExecuteRequestDto"
             ,"RepairOperationDto"
             ,"GuidedRepairDto"
             ,"GuidedRepairStepDto"
             ,"GuidedRepairStepRequestDto"
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
        Assert.Equal(["operationId", "targetId"], PropertyNames(requestSchema));
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

        foreach (var schemaName in new[] { "RedactedConfigurationDto", "RedactedDownloaderConfigurationDto" })
        {
            var configurationFields = PropertyNames(schemas[schemaName]!.AsObject());
            Assert.DoesNotContain(configurationFields, field => field.Equals("sqlPassword", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(configurationFields, field => field.Equals("rdbPassword", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(configurationFields, field => field.Contains("path", StringComparison.OrdinalIgnoreCase));
        }

        var serialized = document.ToJsonString();
        Assert.DoesNotContain("BackupFolder", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("DbFilesPath", serialized, StringComparison.Ordinal);
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

    private static void AssertArrayResponseSchema(
        JsonObject operation,
        string statusCode,
        string mediaType,
        string itemSchemaName)
    {
        var schema = operation["responses"]![statusCode]!["content"]![mediaType]!["schema"]!.AsObject();
        Assert.Equal("array", schema["type"]!.GetValue<string>());
        Assert.Equal(
            $"#/components/schemas/{itemSchemaName}",
            schema["items"]!["$ref"]!.GetValue<string>());
    }

    private static void AssertProtectedReadResponses(JsonObject operation)
    {
        AssertBodylessFrameworkResponse(operation, "401");
        AssertNegotiateChallenge(operation, "401");

        var forbidden = operation["responses"]!["403"]!.AsObject();
        Assert.False(forbidden.ContainsKey("content"));
        Assert.Contains("AuthorizationMiddleware", forbidden["description"]!.GetValue<string>(), StringComparison.Ordinal);

        AssertResponseSchema(operation, "400", "application/problem+json", "AgentProblemDetailsDto");
        Assert.Contains("host_rejected", operation["responses"]!["400"]!["description"]!.GetValue<string>(), StringComparison.Ordinal);
        AssertResponseSchema(operation, "500", "application/problem+json", "AgentProblemDetailsDto");
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

    private static JsonArray ResponseArrayExample(JsonObject operation, string statusCode, string mediaType) =>
        operation["responses"]![statusCode]!["content"]![mediaType]!["example"]!.AsArray();

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
