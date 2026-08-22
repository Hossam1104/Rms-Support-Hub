using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RmsSupportHub.Pos.Agent.Security;
using RmsSupportHub.Pos.Agent.Snapshots;
using RmsSupportHub.Pos.Agent.Support;

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
