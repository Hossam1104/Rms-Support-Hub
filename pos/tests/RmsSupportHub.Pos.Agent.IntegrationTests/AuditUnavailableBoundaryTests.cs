using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RmsSupportHub.Pos.Agent.Artifacts;
using RmsSupportHub.Pos.Agent.Security;
using RmsSupportHub.Pos.Agent.Snapshots;
using RmsSupportHub.Pos.Agent.Support;
using RmsSupportHub.Pos.Agent.IntegrationTests.TestSupport;

namespace RmsSupportHub.Pos.Agent.IntegrationTests;

public sealed class AuditUnavailableBoundaryTests
{
    [Fact]
    public async Task LegacyDiagnosticsMapsMandatoryAuditFailureToTyped503()
    {
        using var factory = new AgentWebApplicationFactory();
        factory.FailDurableAudit();
        using var client = factory.CreateAdminClient();

        using var response = await client.GetAsync("/api/v1/rms/diagnostics");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("audit_unavailable", body.RootElement.GetProperty("code").GetString());
        Assert.DoesNotContain("audit", body.RootElement.GetProperty("title").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("path", body.RootElement.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SupportBundleMapsAuditedDiscoveryFailureToTyped503()
    {
        using var factory = new AgentWebApplicationFactory();
        factory.FailDurableAudit();
        using var client = factory.CreateAdminClient();
        var token = await IssueTokenAsync(client, SupportBundleOperation.OperationId);

        using var request = new HttpRequestMessage(HttpMethod.Post, SupportBundleOperation.HttpPath);
        request.Headers.Add(MutationTokenContract.HeaderName, token);
        using var response = await client.SendAsync(request);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("audit_unavailable", body.RootElement.GetProperty("code").GetString());
        Assert.DoesNotContain("exception", body.RootElement.GetRawText(), StringComparison.OrdinalIgnoreCase);

        var catalog = factory.Services.GetRequiredService<ArtifactCatalog>();
        Assert.Empty(catalog.List(FakeAuthenticationHandler.DefaultSid));
        Assert.Empty(catalog.List("S-1-5-21-1111111111-2222222222-3333333333-1002"));

        using var timelineResponse = await client.GetAsync("/api/v1/diagnostics/timeline");
        Assert.Equal(HttpStatusCode.OK, timelineResponse.StatusCode);
        using var timeline = JsonDocument.Parse(await timelineResponse.Content.ReadAsStringAsync());
        var kinds = timeline.RootElement.GetProperty("events")
            .EnumerateArray()
            .Select(item => item.GetProperty("kind").GetString())
            .ToArray();
        Assert.DoesNotContain("SupportBundle", kinds);
    }

    [Fact]
    public async Task SafetySnapshotPreviewMapsAuditedDiscoveryFailureToTyped503()
    {
        using var factory = new AgentWebApplicationFactory();
        factory.FailDurableAudit();
        using var client = factory.CreateAdminClient();

        using var response = await client.GetAsync("/api/v1/safety-snapshots/preview");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("audit_unavailable", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task SafetySnapshotCaptureMapsAuditedDiscoveryFailureToTyped503()
    {
        using var factory = new AgentWebApplicationFactory();
        factory.FailDurableAudit();
        using var client = factory.CreateAdminClient();
        var token = await IssueTokenAsync(client, SafetySnapshotOperation.CaptureOperationId);

        using var request = new HttpRequestMessage(HttpMethod.Post, SafetySnapshotOperation.CaptureHttpPath)
        {
            Content = JsonContent.Create(new
            {
                typedConfirmation = SafetySnapshotService.ConfirmationPhrase,
                idempotencyKey = "audit-unavailable-capture"
            })
        };
        request.Headers.Add(MutationTokenContract.HeaderName, token);
        using var response = await client.SendAsync(request);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("audit_unavailable", body.RootElement.GetProperty("code").GetString());
    }

    private static async Task<string> IssueTokenAsync(HttpClient client, string operationId)
    {
        using var tokenResponse = await client.PostAsJsonAsync(
            "/api/v1/security/mutation-token",
            new { operationId });
        Assert.Equal(HttpStatusCode.OK, tokenResponse.StatusCode);
        using var document = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("token").GetString()
            ?? throw new InvalidOperationException("The test token was empty.");
    }
}
