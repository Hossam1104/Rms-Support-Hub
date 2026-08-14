using System.Net;
using System.Text.Json;
using RmsSupportHub.Pos.Agent.IntegrationTests.TestSupport;

namespace RmsSupportHub.Pos.Agent.IntegrationTests;

public sealed class ReadOnlyFirstReleaseEndpointTests : IClassFixture<AgentWebApplicationFactory>
{
    private static readonly string[] ProtectedReadPaths =
    [
        "/api/v1/device/identity",
        "/api/v1/device/connectivity",
        "/api/v1/device/capabilities",
        "/api/v1/configuration",
        "/api/v1/services",
        "/api/v1/rms/diagnostics"
    ];

    private readonly AgentWebApplicationFactory _factory;

    public ReadOnlyFirstReleaseEndpointTests(AgentWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AdministratorCanReadEveryFirstReleaseSurface()
    {
        using var client = _factory.CreateAdminClient();

        foreach (var path in ProtectedReadPaths)
        {
            using var response = await client.GetAsync(path);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task NonAdministratorCannotReadProtectedFirstReleaseSurface()
    {
        using var client = _factory.CreateNonAdminClient();

        foreach (var path in ProtectedReadPaths)
        {
            using var response = await client.GetAsync(path);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Null(response.Content.Headers.ContentType?.MediaType);
            Assert.Empty(await response.Content.ReadAsByteArrayAsync());
        }
    }

    [Fact]
    public async Task UnauthenticatedDeviceReadUsesTheNegotiateChallenge()
    {
        using var client = _factory.CreateSecureClient();
        client.DefaultRequestHeaders.Add("Origin", AgentWebApplicationFactory.SupportHubOrigin);

        using var response = await client.GetAsync("/api/v1/device/identity");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(
            response.Headers.WwwAuthenticate,
            challenge => string.Equals(challenge.Scheme, "Negotiate", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task IdentityCapabilitiesAndConnectivityContainOnlySafeDiagnosticData()
    {
        using var client = _factory.CreateAdminClient();

        using var identity = await GetDocumentAsync(client, "/api/v1/device/identity");
        Assert.Equal("BR-INT", identity.RootElement.GetProperty("branchCode").GetString());
        Assert.Equal("POS-07", identity.RootElement.GetProperty("posNumber").GetString());
        Assert.Equal("RMS+ Integration", identity.RootElement.GetProperty("clientName").GetString());
        Assert.DoesNotContain(FakeAuthenticationHandler.DefaultSid, identity.RootElement.GetRawText(), StringComparison.Ordinal);

        using var capabilities = await GetDocumentAsync(client, "/api/v1/device/capabilities");
        Assert.Empty(capabilities.RootElement.GetProperty("browseRoots").EnumerateArray());
        Assert.DoesNotContain("path", capabilities.RootElement.GetRawText(), StringComparison.OrdinalIgnoreCase);

        using var connectivity = await GetDocumentAsync(client, "/api/v1/device/connectivity");
        var connectivityText = connectivity.RootElement.GetRawText();
        Assert.Contains("freshness", connectivityText, StringComparison.Ordinal);
        Assert.DoesNotContain("127.0.0.1", connectivityText, StringComparison.Ordinal);
        Assert.DoesNotContain(FakeAuthenticationHandler.DefaultSid, connectivityText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConfigurationReturnsPresenceFlagsWithoutSecretsOrServerPaths()
    {
        using var client = _factory.CreateAdminClient();

        using var document = await GetDocumentAsync(client, "/api/v1/configuration");
        var root = document.RootElement;
        var json = root.GetRawText();

        Assert.Equal("BR-INT", root.GetProperty("branchCode").GetString());
        Assert.Equal("POS-07", root.GetProperty("posNumber").GetString());
        Assert.False(root.GetProperty("hasSqlPassword").GetBoolean());
        Assert.False(root.GetProperty("downloader").GetProperty("hasRdbPassword").GetBoolean());
        Assert.False(root.TryGetProperty("sqlPassword", out _));
        Assert.False(root.GetProperty("downloader").TryGetProperty("rdbPassword", out _));
        Assert.DoesNotContain("agent-secrets", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DbFilesPath", json, StringComparison.Ordinal);
        Assert.DoesNotContain("BackupFolder", json, StringComparison.Ordinal);
        Assert.DoesNotContain(@"\\rdb\backups", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ServicesExposeOnlyAllowListedTargetsAndStateValidActions()
    {
        using var client = _factory.CreateAdminClient();

        using var document = await GetDocumentAsync(client, "/api/v1/services");
        var services = document.RootElement.EnumerateArray().ToArray();

        Assert.Equal(3, services.Length);
        Assert.Equal("RMS Branch Service", services[0].GetProperty("displayName").GetString());
        Assert.True(services[0].GetProperty("installed").GetBoolean());
        Assert.Equal("running", services[0].GetProperty("state").GetString());
        Assert.Equal(
            ["stop", "restart"],
            services[0].GetProperty("allowedActions").EnumerateArray().Select(value => value.GetString()!).ToArray());
        Assert.Equal("RMS Cashier Service", services[1].GetProperty("displayName").GetString());
        Assert.Equal("stopped", services[1].GetProperty("state").GetString());
        Assert.Equal(
            ["start", "restart"],
            services[1].GetProperty("allowedActions").EnumerateArray().Select(value => value.GetString()!).ToArray());
        Assert.Equal("RMS Services Manager", services[2].GetProperty("displayName").GetString());
        Assert.Equal("stopped", services[2].GetProperty("state").GetString());
        Assert.All(services, service =>
        {
            var serviceId = service.GetProperty("serviceId").GetString();
            Assert.Matches("^svc-[0-9a-f]{16}$", serviceId!);
        });
        Assert.DoesNotContain(FakeAuthenticationHandler.DefaultSid, document.RootElement.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RmsDiagnosticsExposeInstalledEvidenceWithoutSecretBearingConfiguration()
    {
        using var client = _factory.CreateAdminClient();

        using var document = await GetDocumentAsync(client, "/api/v1/rms/diagnostics");
        var root = document.RootElement;
        var json = root.GetRawText();

        Assert.True(root.GetProperty("installation").GetProperty("installed").GetBoolean());
        Assert.Equal("BR-INT", root.GetProperty("installation").GetProperty("branchCode").GetString());
        Assert.Equal("POS-07", root.GetProperty("installation").GetProperty("posNumber").GetString());
        Assert.Equal("consistent", root.GetProperty("installation").GetProperty("consistency").GetProperty("branchCode").GetString());
        Assert.Equal("RmsBranchSrv", root.GetProperty("branchDatabase").GetProperty("expectedDatabase").GetString());
        Assert.Equal("reachable", root.GetProperty("branchDatabase").GetProperty("connectivityStatus").GetString());
        Assert.Equal("RmsCashierSrv", root.GetProperty("cashierDatabase").GetProperty("expectedDatabase").GetString());
        Assert.Equal(3, root.GetProperty("services").GetArrayLength());
        Assert.DoesNotContain("connectionString", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("127.0.0.1", json, StringComparison.Ordinal);
        Assert.DoesNotContain(FakeAuthenticationHandler.DefaultSid, json, StringComparison.Ordinal);
    }

    private static async Task<JsonDocument> GetDocumentAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }
}
