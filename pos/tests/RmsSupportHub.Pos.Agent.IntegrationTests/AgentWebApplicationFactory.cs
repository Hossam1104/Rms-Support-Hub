using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RmsSupportHub.Pos.Agent;
using RmsSupportHub.Pos.Agent.Authorization;
using RmsSupportHub.Pos.Agent.MutationTokens;
using RmsSupportHub.Pos.Agent.IntegrationTests.TestSupport;
using RmsSupportHub.Pos.Agent.Services;
using RmsSupportHub.Pos.Domain.Interfaces;

namespace RmsSupportHub.Pos.Agent.IntegrationTests;

public sealed class AgentWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string SupportHubOrigin = "https://support-hub.integration.test:4443";

    private readonly string _environment;

    public AgentWebApplicationFactory()
        : this(AgentHostConstants.IntegrationTestEnvironment)
    {
    }

    internal AgentWebApplicationFactory(string environment)
    {
        _environment = environment;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environment);
        builder.UseSetting("PosAgentSecurity:SupportHubOrigin", SupportHubOrigin);

        if (!string.Equals(_environment, AgentHostConstants.IntegrationTestEnvironment, StringComparison.Ordinal))
        {
            return;
        }

        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(FakeAuthenticationHandler.SchemeName)
                .AddScheme<FakeAuthenticationOptions, FakeAuthenticationHandler>(
                    FakeAuthenticationHandler.SchemeName,
                    _ => { });

            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultScheme = FakeAuthenticationHandler.SchemeName;
                options.DefaultAuthenticateScheme = FakeAuthenticationHandler.SchemeName;
                options.DefaultChallengeScheme = FakeAuthenticationHandler.SchemeName;
            });

            services.RemoveAll<IAdministratorGroupChecker>();
            services.AddSingleton<IAdministratorGroupChecker, ClaimBasedAdministratorGroupChecker>();

            services.RemoveAll<IAgentConfigurationStore>();
            services.AddSingleton<IAgentConfigurationStore>(new InMemoryAgentConfigurationStore());
            services.RemoveAll<IAgentSecretStore>();
            services.AddSingleton<IAgentSecretStore>(new InMemoryAgentSecretStore());
            services.RemoveAll<IServiceManager>();
            services.AddSingleton<IServiceManager>(new InMemoryServiceManager());

            services.RemoveAll<IRmsInstallationDiscovery>();
            services.AddSingleton<IRmsInstallationDiscovery>(new InMemoryRmsInstallationDiscovery());
            services.RemoveAll<IRmsDatabaseConnectionStringSource>();
            services.RemoveAll<IRmsDatabaseDiagnostics>();
            services.AddSingleton<IRmsDatabaseDiagnostics>(new InMemoryRmsDatabaseDiagnostics());

            services.RemoveAll<IMutationOperationRegistry>();
            services.AddSingleton<IMutationOperationRegistry>(new MutationOperationRegistry(
            [
                ServiceActionOperation.Descriptor,
                new MutationOperationDescriptor("integration.test-mutation", "PUT")
            ]));
        });
    }

    public HttpClient CreateSecureClient()
    {
        return CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri(AgentHostConstants.CanonicalOrigin)
        });
    }

    public HttpClient CreateAdminClient(string principalName = "TESTDOMAIN\\admin-user")
    {
        var client = CreateSecureClient();
        AddBrowserHeaders(client, principalName, FakeAuthenticationHandler.DefaultSid, isAdministrator: true);
        return client;
    }

    public HttpClient CreateNonAdminClient(string principalName = "TESTDOMAIN\\standard-user")
    {
        var client = CreateSecureClient();
        AddBrowserHeaders(client, principalName, FakeAuthenticationHandler.DefaultSid, isAdministrator: false);
        return client;
    }

    public HttpClient CreateClientWithSid(string sid, bool isAdministrator = true)
    {
        var client = CreateSecureClient();
        AddBrowserHeaders(client, "TESTDOMAIN\\sid-user", sid, isAdministrator);
        return client;
    }

    private static void AddBrowserHeaders(HttpClient client, string principalName, string sid, bool isAdministrator)
    {
        client.DefaultRequestHeaders.Add(FakeAuthenticationHandler.PrincipalNameHeader, principalName);
        client.DefaultRequestHeaders.Add(FakeAuthenticationHandler.PrincipalSidHeader, sid);
        client.DefaultRequestHeaders.Add(FakeAuthenticationHandler.IsAdministratorHeader, isAdministrator ? "true" : "false");
        client.DefaultRequestHeaders.Add("Origin", SupportHubOrigin);
    }
}
