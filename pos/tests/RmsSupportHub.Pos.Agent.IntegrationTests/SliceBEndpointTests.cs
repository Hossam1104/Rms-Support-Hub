using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RmsSupportHub.Pos.Agent.MutationTokens;
using RmsSupportHub.Pos.Agent.Security;
using RmsSupportHub.Pos.Agent.Snapshots;
using RmsSupportHub.Pos.Contracts.V1.Snapshots;

namespace RmsSupportHub.Pos.Agent.IntegrationTests;

public sealed class SliceBEndpointTests : IClassFixture<AgentWebApplicationFactory>
{
    private readonly AgentWebApplicationFactory factory;

    public SliceBEndpointTests(AgentWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task MainServerReadsExposeOnlyTheFixedTestingProfileAndFailClosedWhenBindingIsUnavailable()
    {
        using var client = factory.CreateAdminClient();

        var profiles = await client.GetFromJsonAsync<JsonElement>("/api/v1/main-server/profiles");
        var state = await client.GetFromJsonAsync<JsonElement>("/api/v1/main-server/state");

        Assert.Equal("main-server-testing", profiles.GetProperty("activeProfileId").GetString());
        Assert.Equal("mismatch", profiles.GetProperty("activeBinding").GetString());
        Assert.Equal("testing", profiles.GetProperty("profiles")[0].GetProperty("environment").GetString());
        Assert.Equal("notAttempted", state.GetProperty("outcome").GetString());
        Assert.Null(state.GetProperty("branchState").GetString());
        Assert.Null(state.GetProperty("posState").GetString());
        Assert.DoesNotContain("password", profiles.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("connection", profiles.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SnapshotPreviewAndCaptureAreTestingOnlyAndPrincipalScoped()
    {
        using var client = factory.CreateAdminClient();

        var preview = await client.GetFromJsonAsync<JsonElement>("/api/v1/safety-snapshots/preview");
        Assert.Contains("credentials", preview.GetProperty("excludedEvidence").GetRawText(), StringComparison.OrdinalIgnoreCase);

        var token = await IssueTokenAsync(client, SafetySnapshotOperation.CaptureOperationId);
        client.DefaultRequestHeaders.Add(MutationTokenContract.HeaderName, token);
        using var response = await client.PostAsJsonAsync(
            SafetySnapshotOperation.CaptureHttpPath,
            new SafetySnapshotCaptureRequestDto(
                SafetySnapshotService.ConfirmationPhrase,
                "slice-b-snapshot-test-00000000000000000000000000000001"),
            new CancellationToken());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var snapshot = await response.Content.ReadFromJsonAsync<JsonElement>();
        var snapshotId = snapshot.GetProperty("snapshotId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(snapshotId));

        using var verify = await client.GetAsync($"/api/v1/safety-snapshots/{snapshotId}/verify");
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        var verification = await verify.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(verification.GetProperty("verified").GetBoolean());
        Assert.Equal("verified", verification.GetProperty("state").GetString());
        Assert.DoesNotContain("private key", snapshot.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sql", snapshot.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConsolePackageAndRepairPreviewsRemainBlockedWithoutARealExecutableOrPackage()
    {
        using var client = factory.CreateAdminClient();

        var console = await PostPreviewAsync(client, "/api/v1/diagnostic-console/preview/branchServerApi", "console-preview-00000000000000000000000000000001");
        var package = await PostPreviewAsync(client, "/api/v1/packages/preview/upgrade", "package-preview-00000000000000000000000000000001");
        var repair = await PostPreviewAsync(client, "/api/v1/repair/preview/repair", "repair-preview-00000000000000000000000000000001");
        var guided = await PostPreviewAsync(client, "/api/v1/repair/guided/preview", "guided-preview-00000000000000000000000000000001");

        Assert.False(console.GetProperty("ready").GetBoolean());
        Assert.Equal("notAttempted", console.GetProperty("state").GetString());
        Assert.False(package.GetProperty("ready").GetBoolean());
        Assert.Contains("package", package.GetProperty("blockers").GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.False(repair.GetProperty("ready").GetBoolean());
        var guidedSteps = guided.GetProperty("steps").EnumerateArray().ToArray();
        Assert.NotEmpty(guidedSteps);
        Assert.Equal("blocked", guidedSteps[0].GetProperty("state").GetString());
        Assert.DoesNotContain("ProcessStartInfo", console.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("\\Program Files", console.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> IssueTokenAsync(HttpClient client, string operationId)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/v1/security/mutation-token",
            new { operationId });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return body.RootElement.GetProperty("token").GetString()!;
    }

    private static async Task<JsonElement> PostPreviewAsync(HttpClient client, string path, string idempotencyKey)
    {
        using var response = await client.PostAsJsonAsync(path, new { idempotencyKey });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();
    }
}
