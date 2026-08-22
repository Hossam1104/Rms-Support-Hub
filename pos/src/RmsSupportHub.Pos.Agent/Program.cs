using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Scalar.AspNetCore;
using RmsSupportHub.Pos.Agent;
using RmsSupportHub.Pos.Agent.Artifacts;
using RmsSupportHub.Pos.Agent.Authorization;
using RmsSupportHub.Pos.Agent.Console;
using RmsSupportHub.Pos.Agent.Correlation;
using RmsSupportHub.Pos.Agent.Device;
using RmsSupportHub.Pos.Agent.Diagnostics;
using RmsSupportHub.Pos.Agent.Endpoints;
using RmsSupportHub.Pos.Agent.MainServer;
using RmsSupportHub.Pos.Agent.MutationTokens;
using RmsSupportHub.Pos.Agent.Invocation;
using RmsSupportHub.Pos.Agent.LocalIpc;
using RmsSupportHub.Pos.Agent.Packages;
using RmsSupportHub.Pos.Agent.Repair;
using RmsSupportHub.Pos.Agent.RmsDatabase;
using RmsSupportHub.Pos.Agent.Runtime;
using RmsSupportHub.Pos.Agent.Security;
using RmsSupportHub.Pos.Agent.Services;
using RmsSupportHub.Pos.Agent.Support;
using RmsSupportHub.Pos.Agent.Snapshots;
using RmsSupportHub.Pos.Agent.Rms;
using RmsSupportHub.Pos.Application.UseCases;
using RmsSupportHub.Pos.Application.Services;
using RmsSupportHub.Pos.Application.Diagnostics;
using RmsSupportHub.Pos.Application.Maintenance;
using RmsSupportHub.Pos.Application.Packages;
using RmsSupportHub.Pos.Application.Repair;
using RmsSupportHub.Pos.Contracts.V1.Common;
using RmsSupportHub.Pos.Contracts.V1.Security;
using RmsSupportHub.Pos.Contracts.V1.Session;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;
using RmsSupportHub.Pos.Infrastructure.Backups;
using RmsSupportHub.Pos.Infrastructure.Audit;
using RmsSupportHub.Pos.Infrastructure.Configuration;
using RmsSupportHub.Pos.Infrastructure.Diagnostics;
using RmsSupportHub.Pos.Infrastructure.Databases;
using RmsSupportHub.Pos.Infrastructure.Installation;
using RmsSupportHub.Pos.Infrastructure.MainServer;
using RmsSupportHub.Pos.Infrastructure.Http;
using RmsSupportHub.Pos.Infrastructure.Packages;
using RmsSupportHub.Pos.Infrastructure.Snapshots;
using RmsSupportHub.Pos.Infrastructure.Smb;
using RmsSupportHub.Pos.Infrastructure.Windows;
using RmsSupportHub.Pos.LocalIpc;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});
var isMetadataOnlyOpenApi = AgentOpenApiHost.IsMetadataOnlyGeneration();

var securityOptions = builder.Configuration
    .GetSection(AgentSecurityOptions.SectionName)
    .Get<AgentSecurityOptions>() ?? new AgentSecurityOptions();
securityOptions.Validate();

var localIpcOptions = builder.Configuration
    .GetSection(LocalIpcOptions.SectionName)
    .Get<LocalIpcOptions>() ?? new LocalIpcOptions();
localIpcOptions.Validate();

builder.Host.UseWindowsService(options => options.ServiceName = AgentProductIdentity.PermanentServiceName);

builder.WebHost.ConfigureKestrel((context, options) =>
    LoopbackBinding.Configure(options, context.Configuration, context.HostingEnvironment));

builder.Services.AddSingleton(securityOptions);
builder.Services.AddSingleton(localIpcOptions);
builder.Services.AddSingleton<LocalIpcRuntimeStatus>();
builder.Services.AddSingleton<ILocalIpcAccountSidResolver, WindowsLocalIpcAccountSidResolver>();
builder.Services.AddSingleton<ILocalIpcOperatorGroupResolver, WindowsLocalIpcOperatorGroupResolver>();
builder.Services.AddSingleton<ILocalIpcSecurityDescriptorFactory, WindowsLocalIpcSecurityDescriptorFactory>();
builder.Services.AddSingleton<LocalIpcServer>();
builder.Services.AddHostedService(services => services.GetRequiredService<LocalIpcServer>());
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(RuntimeRetentionPolicy.Default);
builder.Services.AddSingleton<AgentScopedIdempotencyStore>();
if (!isMetadataOnlyOpenApi)
{
    builder.Services.AddSingleton<IPrivilegedMutationLease, MachineWidePrivilegedMutationLease>();
}

// The real process always uses Negotiate. IntegrationTest is a dedicated host environment used by
// the test project to substitute a fake scheme because TestServer cannot perform SSPI handshakes;
// no ordinary configuration key can disable production authentication.
var authentication = builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = NegotiateDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = NegotiateDefaults.AuthenticationScheme;
});
if (!builder.Environment.IsEnvironment(AgentHostConstants.IntegrationTestEnvironment))
{
    authentication.AddNegotiate();
}

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(PolicyNames.LocalAdministratorsOnly, policy => policy
        .RequireAuthenticatedUser()
        .Requirements.Add(new LocalAdministratorsOnlyRequirement()));
});
builder.Services.AddSingleton<IAuthorizationHandler, LocalAdministratorsOnlyHandler>();
builder.Services.AddSingleton<IAdministratorGroupChecker, WindowsAdministratorGroupChecker>();
builder.Services.AddSingleton<IWindowsLocalGroupMembershipResolver, WindowsLocalGroupMembershipResolver>();
builder.Services.AddSingleton<IWindowsLocalGroupMembershipLookup, WindowsLocalGroupMembershipLookup>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IAgentPrincipalSidResolver, AgentPrincipalSidResolver>();
builder.Services.AddSingleton<IAgentInvocationContextFactory, AgentInvocationContextFactory>();

builder.Services.AddCors(options => options.AddPolicy(AgentCors.PolicyName, policy =>
{
    policy.WithOrigins(securityOptions.SupportHubOrigin!)
        .WithMethods(AgentCors.AllowedMethods)
        .WithHeaders(AgentCors.AllowedHeaders)
        .AllowCredentials();
}));

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        var correlationId = CorrelationIdContext.TryGet(context.HttpContext);
        if (correlationId is not null)
        {
            context.ProblemDetails.Extensions["correlationId"] = correlationId;
        }
    };
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});

builder.Services.AddSingleton(new MutationTokenOptions());
builder.Services.AddSingleton<InMemoryMutationTokenStore>();
builder.Services.AddSingleton<IMutationTokenStore>(services =>
    services.GetRequiredService<InMemoryMutationTokenStore>());
builder.Services.AddSingleton<MutationTokenService>();
builder.Services.AddSingleton<IMutationOperationRegistry>(new MutationOperationRegistry(
[
    ServiceActionOperation.Descriptor,
    RmsDatabaseOperation.BackupDescriptor,
    RmsDatabaseOperation.RestoreDescriptor,
    DownloaderOperation.Descriptor,
    MaintenanceOperation.CleanupDescriptor,
    MaintenanceOperation.BranchResetDescriptor,
    SupportBundleOperation.Descriptor,
    SafetySnapshotOperation.Descriptor,
    DiagnosticConsoleOperation.Descriptor,
    AgentPackageOperation.Descriptor,
    RepairOperation.Descriptor,
    GuidedRepairOperation.Descriptor
]));

builder.Services.AddOpenApi("v1", options =>
{
    options.AddOperationTransformer<AgentOpenApiOperationTransformer>();
    options.AddSchemaTransformer<AgentOpenApiSchemaTransformer>();
    options.AddDocumentTransformer<AgentOpenApiDocumentTransformer>();
});

// The Agent composes only typed, server-owned capabilities. Legacy WinUI configuration importers,
// generic SQL/command surfaces, and caller-selected filesystem workflows remain outside this
// process boundary.
builder.Services.AddSingleton(new AgentConfigurationStoreOptions());
builder.Services.AddSingleton<IAgentConfigurationStore, JsonAgentConfigurationStore>();
builder.Services.AddSingleton<IAgentSecretStore, DpapiAgentSecretStore>();
builder.Services.AddSingleton<AgentConfigurationUseCase>();
builder.Services.AddSingleton<AgentRuntimeSettingsFactory>();
builder.Services.AddSingleton(new RmsInstallationOptions());
builder.Services.AddSingleton<RmsInstallationDiscovery>();
builder.Services.AddSingleton<IRmsInstallationDiscovery>(services =>
    services.GetRequiredService<RmsInstallationDiscovery>());
builder.Services.AddSingleton<RmsInstallationDiscoveryQueryHandler>();
builder.Services.AddSingleton<IRmsDatabaseConnectionStringSource>(services =>
    services.GetRequiredService<RmsInstallationDiscovery>());
builder.Services.AddSingleton<IRmsSqlReadOnlyProbe, SqlClientReadOnlyProbe>();
builder.Services.AddSingleton<IRmsDatabaseDiagnostics, RmsDatabaseDiagnostics>();
builder.Services.AddSingleton(new RmsDiagnosticEvidenceOptions());
builder.Services.AddSingleton<IRmsDiagnosticEvidenceReader, WindowsRmsDiagnosticEvidenceReader>();
builder.Services.AddSingleton(new RmsFixedHealthOptions());
builder.Services.AddSingleton<IRmsFixedHealthReader, WindowsRmsFixedHealthReader>();
builder.Services.AddSingleton<ServiceFailureAnalyzer>();
builder.Services.AddSingleton<IncidentTimelineStore>();
builder.Services.AddSingleton<IncidentTimelineService>();
builder.Services.AddSingleton<RmsConnectivityDiagnostics>();
builder.Services.AddSingleton<RmsDiagnosticsService>();
builder.Services.AddSingleton<RmsOperationalHealthService>();
builder.Services.AddSingleton<RmsDatabaseHealthService>();
builder.Services.AddSingleton<PosHealthService>();
builder.Services.AddSingleton(new SupportBundleOptions());
builder.Services.AddSingleton<SupportBundleService>();
builder.Services.AddSingleton<SupportBundleRuntime>();
builder.Services.AddSingleton<DeviceDiagnosticsService>();
builder.Services.AddSingleton<IServiceManager, WindowsServiceManager>();
builder.Services.AddSingleton<ServiceAllowList>();
builder.Services.AddSingleton<ReadOnlyServiceStatusService>();
builder.Services.AddSingleton(new ServiceActionOptions());
builder.Services.AddSingleton<ServiceActionIdempotencyStore>();
builder.Services.AddSingleton<ServiceActionConcurrencyGate>();
builder.Services.AddSingleton<IMutationOperationTargetResolver, MutationOperationTargetResolver>();
builder.Services.AddSingleton<ServiceActionRuntime>();

// Downloader and maintenance are composed from the already-tested typed Application and
// Infrastructure seams. Their configuration and credentials are projected server-side by
// AgentRuntimeSettingsFactory; no browser request can replace these adapters.
builder.Services.AddSingleton<IHostAddressResolver, SystemHostAddressResolver>();
builder.Services.AddSingleton<HttpClient>(services =>
    new HttpClient(BackupApiHttpMessageHandlerFactory.Create(
        services.GetRequiredService<IHostAddressResolver>())));
builder.Services.AddSingleton<SqlCmdExecutor>();
builder.Services.AddSingleton<IDatabaseService>(services => services.GetRequiredService<SqlCmdExecutor>());
builder.Services.AddSingleton<IMaintenanceDatabasePreview>(services => services.GetRequiredService<SqlCmdExecutor>());
builder.Services.AddSingleton<IMaintenanceDatabaseReset>(services => services.GetRequiredService<SqlCmdExecutor>());
builder.Services.AddSingleton<IMaintenanceFileSystem, PhysicalMaintenanceFileSystem>();
builder.Services.AddSingleton<IBackupApiClient, BackupApiClient>();
builder.Services.AddSingleton<IBackupRepository, SmbBackupRepository>();
builder.Services.AddSingleton<DbDownloadService>();
builder.Services.AddSingleton<MaintenanceService>();
builder.Services.AddSingleton<DownloaderOperationStore>();
builder.Services.AddSingleton<DownloaderIdempotencyStore>();
builder.Services.AddSingleton<MaintenanceOperationStore>();
builder.Services.AddSingleton<MaintenanceIdempotencyStore>();
builder.Services.AddSingleton<MaintenanceChallengeStore>();
builder.Services.AddSingleton<AgentOperationConcurrencyGate>();
builder.Services.AddSingleton<DownloaderOperationRuntime>();
builder.Services.AddSingleton<MaintenanceOperationRuntime>();

// Artifacts are process-local, principal-scoped capabilities. The endpoint exposes only the
// generated opaque identifier and a sanitized download response.
builder.Services.AddSingleton<IBackupFileSystem, PhysicalBackupFileSystem>();
builder.Services.AddSingleton<ArtifactCatalog>();
builder.Services.AddSingleton(new RmsDatabaseStorageOptions());
builder.Services.AddSingleton<IRmsDatabaseSqlOperations, RmsSqlDatabaseOperations>();
// RmsDatabaseBackupCatalog is the durable, Agent-owned record of approved database backups: it
// persists to disk beneath the backup root so backups remain discoverable after an Agent restart,
// independently of ArtifactCatalog's in-memory browser-download lifetime.
builder.Services.AddSingleton<RmsDatabaseBackupCatalog>();
builder.Services.AddSingleton<IRmsDatabaseBackupStorage, RmsDatabaseBackupStorage>();
builder.Services.AddSingleton<IRmsDatabaseWorkflow, RmsDatabaseWorkflowService>();
builder.Services.AddSingleton<RmsDatabaseOperationStore>();
builder.Services.AddSingleton<RmsDatabaseIdempotencyStore>();
builder.Services.AddSingleton<RmsDatabaseConcurrencyGate>();
builder.Services.AddSingleton(new AgentAuditOptions());
builder.Services.AddSingleton<FileAgentAuditSink>();
builder.Services.AddSingleton<IAgentAuditSink>(services => services.GetRequiredService<FileAgentAuditSink>());
builder.Services.AddSingleton<IAgentAuditReader>(services => services.GetRequiredService<FileAgentAuditSink>());
builder.Services.AddSingleton<IRmsPrivilegedAuditSink>(services => services.GetRequiredService<FileAgentAuditSink>());
builder.Services.AddSingleton<RmsDatabaseOperationRuntime>();

// Slice B server-owned Main Server profiles. The dedicated client is constructed separately
// from the backup client so no browser-selected URL, header, or route can reach this boundary.
builder.Services.AddSingleton(new MainServerProfileOptions());
builder.Services.AddSingleton<IMainServerProfileCatalog, MainServerProfileCatalog>();
builder.Services.AddSingleton<MainServerHttpClient>(_ =>
{
    var client = new HttpClient(MainServerHttpMessageHandlerFactory.Create())
    {
        Timeout = TimeSpan.FromSeconds(15)
    };
    return new MainServerHttpClient(client);
});
builder.Services.AddSingleton<IMainServerReadOnlyClient>(services => services.GetRequiredService<MainServerHttpClient>());
builder.Services.AddSingleton<MainServerService>();

// Safe Diagnostic Console: all process details come from the fixed manifest and installation
// discovery. The real runner is never selected by browser data and remains a testable seam.
builder.Services.AddSingleton<IDiagnosticConsoleManifest, DiagnosticConsoleManifest>();
builder.Services.AddSingleton<IDiagnosticConsolePolicy, DiagnosticConsolePolicy>();
builder.Services.AddSingleton<IConstrainedDiagnosticProcessRunner, ConstrainedDiagnosticProcessRunner>();
builder.Services.AddSingleton<IDiagnosticConsoleOutputRedactor, DiagnosticOutputRedactor>();
builder.Services.AddSingleton(new DiagnosticConsoleOptions());
builder.Services.AddSingleton<DiagnosticConsolePreviewStore>();
builder.Services.AddSingleton<DiagnosticConsoleRunStore>();
builder.Services.AddSingleton<DiagnosticConsoleRuntime>();

// Safety snapshots are fixed-root, atomic, integrity-protected Agent records. Their evidence
// source is composed from the existing safe Slice A diagnostics projection.
builder.Services.AddSingleton(new SafetySnapshotOptions
{
    EnvironmentName = builder.Environment.EnvironmentName,
    ProfileId = builder.Configuration["PosAgent:ProfileId"]
});
builder.Services.AddSingleton<ISafetySnapshotStore, FileSafetySnapshotStore>();
builder.Services.AddSingleton<IAuthorizedSafetySnapshotEvidenceSource, RmsSafetySnapshotEvidenceSource>();
builder.Services.AddSingleton<SafetySnapshotService>();

if (!isMetadataOnlyOpenApi)
{
    // Obsolete trust-related configuration is rejected by presence, including an empty value. The
    // canonical machine trust file is the only normal runtime authority; these keys can never name
    // an alternate file or influence the snapshot.
    foreach (var obsoleteKey in new[] { "PosAgent:ReleaseChannel", "PosAgent:TrustConfigurationPath" })
    {
        if (builder.Configuration.AsEnumerable(true).Any(item =>
                string.Equals(item.Key, obsoleteKey, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"{obsoleteKey} is obsolete and rejected. Agent package trust is determined exclusively by the canonical machine-owned package-trust.json.");
        }
    }

    // Normal Agent composition has one loader with one authority. The snapshot is resolved before
    // startup completes so a missing, malformed, or untrusted canonical file fails closed. Tests
    // can replace this loader only through their dedicated DI seam; no normal configuration path
    // reaches the loader.
    builder.Services.AddSingleton<IAgentMachineTrustConfigurationLoader, MachineAgentTrustConfigurationLoader>();
    builder.Services.AddSingleton<AgentMachineTrustConfiguration>(services =>
        services.GetRequiredService<IAgentMachineTrustConfigurationLoader>().Load());
    builder.Services.AddSingleton<AgentPackageOptions>(services =>
    {
        var machineTrust = services.GetRequiredService<AgentMachineTrustConfiguration>();
        var options = new AgentPackageOptions { ReleaseChannel = machineTrust.DeploymentMode };
        options.Validate();
        return options;
    });
    builder.Services.AddSingleton<AgentPackageTrustOptions>(services =>
    {
        var machineTrust = services.GetRequiredService<AgentMachineTrustConfiguration>();
        var options = new AgentPackageTrustOptions
        {
            ProductionSignerThumbprint = machineTrust.ProductionSignerThumbprint,
            TestingSignerThumbprint = machineTrust.TestingSignerThumbprint
        };
        options.Validate();
        return options;
    });
    builder.Services.AddSingleton<IAgentPackageSignerCertificateSource, LocalMachineAgentPackageSignerCertificateSource>();
    builder.Services.AddSingleton<IAgentPackageSignerTrustValidator, X509ChainAgentPackageSignerTrustValidator>();
    builder.Services.AddSingleton<IAgentPackagePolicy, AgentPackagePolicy>();
    builder.Services.AddSingleton<IAgentPackageCatalog, FileAgentPackageCatalog>();
    builder.Services.AddSingleton<IAgentPackageSignatureVerifier, MachineCertificatePackageSignatureVerifier>();
    builder.Services.AddSingleton<FileAgentPackageVerifier>();
    builder.Services.AddSingleton<IAgentPackageVerifier>(services => services.GetRequiredService<FileAgentPackageVerifier>());
    builder.Services.AddSingleton(new AgentCertificatePrerequisiteOptions { Thumbprint = securityOptions.CertificateThumbprint });
    builder.Services.AddSingleton<IAgentCertificatePrerequisite, MachineAgentCertificatePrerequisite>();
    builder.Services.AddSingleton<IAgentServiceLifecycleController, WindowsAgentServiceController>();
    builder.Services.AddSingleton<AgentPackageLifecycleStateStore>();
    builder.Services.AddSingleton<IAgentHealthProbe, LoopbackAgentHealthProbe>();
    builder.Services.AddSingleton<IAgentPackageInstallationPlatform, WindowsAgentPackageInstallationPlatform>();
    builder.Services.AddSingleton<FileAgentPackageLifecycle>();
    builder.Services.AddSingleton<IAgentPackageLifecycle>(services => services.GetRequiredService<FileAgentPackageLifecycle>());
    builder.Services.AddSingleton<AgentPackagePreviewStore>();
    builder.Services.AddSingleton<AgentPackageOperationStore>();
    builder.Services.AddSingleton<AgentPackageService>();

    // Repair and Guided Repair reuse the same package lifecycle and snapshot verification boundary.
    builder.Services.AddSingleton<RepairPreviewStore>();
    builder.Services.AddSingleton<RepairOperationStore>();
    builder.Services.AddSingleton<GuidedRepairStore>();
    builder.Services.AddSingleton<RepairService>();
}
else
{
    // Minimal API endpoint metadata needs these parameter types to be known as services while the
    // document generator reflects routes. The metadata host registers deliberate poison-pill
    // descriptors: no package/repair service can be constructed or used in this process.
    AgentOpenApiHost.AddMetadataOnlyLifecycleGuards(builder.Services);
}

var app = builder.Build();

if (!isMetadataOnlyOpenApi)
{
    // Force the immutable trust snapshot to be validated during normal startup. The metadata host
    // intentionally has no snapshot or lifecycle registrations and therefore never reaches here.
    _ = app.Services.GetRequiredService<AgentMachineTrustConfiguration>();
}

app.UseExceptionHandler();
app.UseMiddleware<CanonicalHostValidationMiddleware>();
app.UseMiddleware<HttpsOnlyMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseRouting();

// CORS must run before authentication so a valid exact-origin OPTIONS request remains anonymous.
app.UseMiddleware<CorsPreflightValidationMiddleware>();
app.UseCors(AgentCors.PolicyName);
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<ExactOriginMiddleware>();

app.MapGet("/health/live", () => Results.Ok(new HealthStatusDto("live")))
    .AllowAnonymous()
    .WithName("GetHealthLive")
    .WithTags("Health")
    .WithSummary("Check Agent liveness")
    .WithDescription(
        "Confirms that the local POS Agent process is alive and able to answer HTTP requests. " +
        "This anonymous check has no side effects and does not prove POS SQL, SCM, SMB, backup, " +
        "restore, browser authentication, or mutation authorization readiness.")
    .Produces<HealthStatusDto>(StatusCodes.Status200OK)
    .Produces<AgentProblemDetailsDto>(StatusCodes.Status400BadRequest, "application/problem+json");
app.MapGet("/health/ready", () => Results.Ok(new HealthStatusDto("ready")))
    .AllowAnonymous()
    .WithName("GetHealthReady")
    .WithTags("Health")
    .WithSummary("Check Agent readiness")
    .WithDescription(
        "Returns the current foundation-stage readiness response. This endpoint is anonymous, has " +
        "no side effects, and currently has the same implementation behavior as the liveness " +
        "endpoint; it does not probe POS SQL, SCM, SMB, backup, restore, or feature readiness.")
    .Produces<HealthStatusDto>(StatusCodes.Status200OK)
    .Produces<AgentProblemDetailsDto>(StatusCodes.Status400BadRequest, "application/problem+json");

app.MapGet(
        "/api/v1/session",
        (HttpContext context,
            IAdministratorGroupChecker administratorGroupChecker,
            IAgentPrincipalSidResolver principalSidResolver) =>
    {
        if (!principalSidResolver.TryGetSid(context.User, out _))
        {
            return AgentProblemDetails.CreateResult(
                context,
                StatusCodes.Status403Forbidden,
                "The authenticated Windows SID could not be resolved.",
                AgentProblemCodes.WindowsSidUnavailable);
        }

        var principalName = context.User.Identity?.Name ?? "authenticated Windows user";
        var response = new SessionInfoDto(
            principalName,
            administratorGroupChecker.IsInAdministratorsGroup(context.User),
            typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "0.0.0",
            AgentHostConstants.ApiVersion,
            [AgentHostConstants.ApiVersion]);

        return Results.Ok(response);
    })
    .RequireAuthorization()
    .WithName("GetAgentSession")
    .WithTags("Session")
    .WithSummary("Read authenticated Agent session diagnostics")
    .WithDescription(
        "Returns security and API-version diagnostics for the Windows account authenticated by " +
        "Negotiate. Any authenticated Windows account with a resolvable SID may call this endpoint. " +
        "IsAuthorized represents membership in the local Built-in Administrators group resolved by " +
        "Windows independently of UAC browser-token elevation; the raw Windows SID is never returned. " +
        "The endpoint has no side effects.")
    .Produces<SessionInfoDto>(StatusCodes.Status200OK)
    .Produces<AgentProblemDetailsDto>(StatusCodes.Status400BadRequest, "application/problem+json")
    .Produces(StatusCodes.Status401Unauthorized)
    .Produces<AgentProblemDetailsDto>(StatusCodes.Status403Forbidden, "application/problem+json");

app.MapPost(
        "/api/v1/security/mutation-token",
        async (HttpContext context,
            MutationTokenIssueRequestDto request,
            IMutationOperationRegistry operationRegistry,
            MutationTokenService mutationTokenService,
            IAgentPrincipalSidResolver principalSidResolver,
            CancellationToken cancellationToken) =>
        {
            if (!principalSidResolver.TryGetSid(context.User, out _))
            {
                return AgentProblemDetails.CreateResult(
                    context,
                    StatusCodes.Status403Forbidden,
                    "The authenticated Windows SID could not be resolved.",
                    AgentProblemCodes.WindowsSidUnavailable);
            }

            if (!operationRegistry.TryGet(request.OperationId, out var operation))
            {
                return AgentProblemDetails.CreateResult(
                    context,
                    StatusCodes.Status400BadRequest,
                    "The requested mutation operation is not supported.",
                    AgentProblemCodes.OperationNotSupported);
            }

            try
            {
                var issued = await mutationTokenService.IssueAsync(
                    context,
                    operation,
                    request.TargetId,
                    cancellationToken).ConfigureAwait(false);
                return Results.Ok(new MutationTokenIssueResponseDto(issued.Token, issued.ExpiresAtUtc));
            }
            catch (MutationTargetRejectedException)
            {
                return AgentProblemDetails.CreateResult(
                    context,
                    StatusCodes.Status400BadRequest,
                    "The requested mutation target is not available.",
                    AgentProblemCodes.MutationTargetInvalid);
            }
            catch (MutationTokenCapacityException)
            {
                return AgentProblemDetails.CreateResult(
                    context,
                    StatusCodes.Status429TooManyRequests,
                    "The mutation-token retention limit has been reached.",
                    AgentProblemCodes.MutationTokenCapacity);
            }
        })
    .RequireAuthorization(PolicyNames.LocalAdministratorsOnly)
    .WithName("IssueMutationToken")
    .WithTags("Security")
    .WithSummary("Issue a one-use mutation authorization token")
    .WithDescription(
        "Issues a short-lived, one-use token for one server-registered mutation operation. Windows " +
        "Negotiate authentication and local Built-in Administrators membership are required; normal " +
        "browser elevation is not. The browser supplies only a logical operationId, while the server " +
        "registry resolves the target allow-list entry, method, and path. The token is bound to the " +
        "authenticated Windows SID, exact Support Hub Origin, operation, target path, and " +
        "server-resolved method, and is replay protected in memory. Unknown or unavailable " +
        "operations/targets return safe problem details without issuing a token. Issuance changes " +
        "only short-lived in-memory token state; it does not perform the POS mutation.")
    .Accepts<MutationTokenIssueRequestDto>("application/json")
    .Produces<MutationTokenIssueResponseDto>(StatusCodes.Status200OK)
        .Produces<AgentProblemDetailsDto>(StatusCodes.Status400BadRequest, "application/problem+json")
    .Produces(StatusCodes.Status401Unauthorized)
    .Produces(StatusCodes.Status403Forbidden)
    .Produces<AgentProblemDetailsDto>(StatusCodes.Status429TooManyRequests, "application/problem+json");

app.MapDeviceEndpoints();
app.MapConfigurationEndpoints();
app.MapServiceEndpoints();
app.MapRmsEndpoints();
app.MapDiagnosticsEndpoints();
app.MapTimelineEndpoints();
app.MapSupportBundleEndpoints();
app.MapRmsDatabaseEndpoints();
app.MapDownloaderEndpoints();
app.MapMaintenanceEndpoints();
app.MapArtifactEndpoints();
app.MapMainServerEndpoints();
app.MapSafetySnapshotEndpoints();
app.MapDiagnosticConsoleEndpoints();
app.MapAgentPackageEndpoints();
app.MapRepairEndpoints();

if (app.Environment.IsDevelopment()
    || app.Environment.IsEnvironment(AgentHostConstants.IntegrationTestEnvironment))
{
    app.MapOpenApi("/openapi/{documentName}.json");
    app.MapScalarApiReference(
            "/scalar",
            options => options
                .WithTitle("RMS+ POS Agent API")
                .WithOpenApiRoutePattern("/openapi/{documentName}.json")
                .DisableAgent()
                .DisableDefaultFonts())
        .AllowAnonymous();
}

app.Run();

public partial class Program;
