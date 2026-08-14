using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RmsSupportHub.Pos.Agent;
using RmsSupportHub.Pos.Agent.Artifacts;
using RmsSupportHub.Pos.Agent.Authorization;
using RmsSupportHub.Pos.Agent.MutationTokens;
using RmsSupportHub.Pos.Agent.RmsDatabase;
using RmsSupportHub.Pos.Agent.IntegrationTests.TestSupport;
using RmsSupportHub.Pos.Agent.Runtime;
using RmsSupportHub.Pos.Application.Services;
using RmsSupportHub.Pos.Agent.Services;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Agent.IntegrationTests;

public sealed class AgentWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string SupportHubOrigin = "https://support-hub.integration.test:4443";

    private readonly string _environment;
    private readonly string _databaseStorageRoot = Path.Combine(
        Path.GetTempPath(),
        "RmsSupportHub-Agent-Integration",
        Guid.NewGuid().ToString("N"));
    private readonly InMemoryAgentConfigurationStore _configurationStore;
    private readonly InMemoryAgentSecretStore _secretStore = new();

    public AgentWebApplicationFactory()
        : this(AgentHostConstants.IntegrationTestEnvironment)
    {
    }

    internal AgentWebApplicationFactory(string environment)
    {
        _environment = environment;
        _configurationStore = new InMemoryAgentConfigurationStore(_databaseStorageRoot);
    }

    public void EnableDownloaderCredential() => _secretStore.EnableDownloaderCredential();

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
            services.AddSingleton<IAgentConfigurationStore>(_configurationStore);
            services.RemoveAll<IAgentSecretStore>();
            services.AddSingleton<IAgentSecretStore>(_secretStore);
            services.RemoveAll<IServiceManager>();
            services.AddSingleton<IServiceManager>(new InMemoryServiceManager());

            services.RemoveAll<IBackupApiClient>();
            services.AddSingleton<InMemoryBackupApiClient>();
            services.AddSingleton<IBackupApiClient>(services => services.GetRequiredService<InMemoryBackupApiClient>());
            services.RemoveAll<IBackupRepository>();
            services.AddSingleton<IBackupRepository, InMemoryBackupRepository>();
            services.RemoveAll<DbDownloadService>();
            services.AddSingleton(services => new DbDownloadService(
                services.GetRequiredService<IBackupApiClient>(),
                services.GetRequiredService<IBackupRepository>(),
                TimeProvider.System,
                new ImmediateDownloaderDelay()));

            services.RemoveAll<IDatabaseService>();
            services.RemoveAll<IMaintenanceDatabasePreview>();
            services.RemoveAll<IMaintenanceDatabaseReset>();
            services.RemoveAll<IMaintenanceFileSystem>();
            services.AddSingleton<InMemoryMaintenanceDatabase>();
            services.AddSingleton<IDatabaseService>(services => services.GetRequiredService<InMemoryMaintenanceDatabase>());
            services.AddSingleton<IMaintenanceDatabasePreview>(services => services.GetRequiredService<InMemoryMaintenanceDatabase>());
            services.AddSingleton<IMaintenanceDatabaseReset>(services => services.GetRequiredService<InMemoryMaintenanceDatabase>());
            services.AddSingleton<InMemoryMaintenanceFileSystem>();
            services.AddSingleton<IMaintenanceFileSystem>(services => services.GetRequiredService<InMemoryMaintenanceFileSystem>());

            services.RemoveAll<IRmsInstallationDiscovery>();
            services.AddSingleton<IRmsInstallationDiscovery>(new InMemoryRmsInstallationDiscovery());
            services.RemoveAll<IRmsDatabaseConnectionStringSource>();
            services.RemoveAll<IRmsDatabaseDiagnostics>();
            services.AddSingleton<IRmsDatabaseDiagnostics>(new InMemoryRmsDatabaseDiagnostics());
            services.RemoveAll<RmsDatabaseStorageOptions>();
            services.AddSingleton(new RmsDatabaseStorageOptions
            {
                BackupRootPath = Path.Combine(_databaseStorageRoot, "backups"),
                DatabaseFilesRootPath = Path.Combine(_databaseStorageRoot, "database-files")
            });
            services.RemoveAll<IRmsDatabaseSqlOperations>();
            services.AddSingleton<IRmsDatabaseSqlOperations, InMemoryRmsDatabaseSqlOperations>();

            services.RemoveAll<IMutationOperationRegistry>();
            services.AddSingleton<IMutationOperationRegistry>(new MutationOperationRegistry(
            [
                ServiceActionOperation.Descriptor,
                RmsDatabaseOperation.BackupDescriptor,
                RmsDatabaseOperation.RestoreDescriptor,
                DownloaderOperation.Descriptor,
                MaintenanceOperation.CleanupDescriptor,
                MaintenanceOperation.BranchResetDescriptor,
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
