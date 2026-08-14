using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Scalar.AspNetCore;
using RmsSupportHub.Pos.Agent;
using RmsSupportHub.Pos.Agent.Artifacts;
using RmsSupportHub.Pos.Agent.Authorization;
using RmsSupportHub.Pos.Agent.Correlation;
using RmsSupportHub.Pos.Agent.Device;
using RmsSupportHub.Pos.Agent.Endpoints;
using RmsSupportHub.Pos.Agent.MutationTokens;
using RmsSupportHub.Pos.Agent.RmsDatabase;
using RmsSupportHub.Pos.Agent.Security;
using RmsSupportHub.Pos.Agent.Services;
using RmsSupportHub.Pos.Agent.Rms;
using RmsSupportHub.Pos.Application.UseCases;
using RmsSupportHub.Pos.Application.Services;
using RmsSupportHub.Pos.Contracts.V1.Common;
using RmsSupportHub.Pos.Contracts.V1.Security;
using RmsSupportHub.Pos.Contracts.V1.Session;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;
using RmsSupportHub.Pos.Infrastructure.Backups;
using RmsSupportHub.Pos.Infrastructure.Configuration;
using RmsSupportHub.Pos.Infrastructure.Databases;
using RmsSupportHub.Pos.Infrastructure.Installation;
using RmsSupportHub.Pos.Infrastructure.Windows;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

var securityOptions = builder.Configuration
    .GetSection(AgentSecurityOptions.SectionName)
    .Get<AgentSecurityOptions>() ?? new AgentSecurityOptions();
securityOptions.Validate();

builder.Host.UseWindowsService(options => options.ServiceName = "RmsSupportHub.Pos.Agent");

builder.WebHost.ConfigureKestrel((context, options) =>
    LoopbackBinding.Configure(options, context.Configuration, context.HostingEnvironment));

builder.Services.AddSingleton(securityOptions);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(RuntimeRetentionPolicy.Default);

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
    RmsDatabaseOperation.RestoreDescriptor
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
builder.Services.AddSingleton(new RmsInstallationOptions());
builder.Services.AddSingleton<RmsInstallationDiscovery>();
builder.Services.AddSingleton<IRmsInstallationDiscovery>(services =>
    services.GetRequiredService<RmsInstallationDiscovery>());
builder.Services.AddSingleton<IRmsDatabaseConnectionStringSource>(services =>
    services.GetRequiredService<RmsInstallationDiscovery>());
builder.Services.AddSingleton<IRmsSqlReadOnlyProbe, SqlClientReadOnlyProbe>();
builder.Services.AddSingleton<IRmsDatabaseDiagnostics, RmsDatabaseDiagnostics>();
builder.Services.AddSingleton<RmsConnectivityDiagnostics>();
builder.Services.AddSingleton<RmsDiagnosticsService>();
builder.Services.AddSingleton<DeviceDiagnosticsService>();
builder.Services.AddSingleton<IServiceManager, WindowsServiceManager>();
builder.Services.AddSingleton<ServiceAllowList>();
builder.Services.AddSingleton<ReadOnlyServiceStatusService>();
builder.Services.AddSingleton(new ServiceActionOptions());
builder.Services.AddSingleton<ServiceActionIdempotencyStore>();
builder.Services.AddSingleton<ServiceActionConcurrencyGate>();
builder.Services.AddSingleton<IMutationOperationTargetResolver, MutationOperationTargetResolver>();
builder.Services.AddSingleton<ServiceActionRuntime>();

// ArtifactCatalog is retained as a process-local foundation, but no HTTP artifact endpoint is
// mapped in this session. Its file capability remains behind the existing Infrastructure port.
builder.Services.AddSingleton<IBackupFileSystem, PhysicalBackupFileSystem>();
builder.Services.AddSingleton<ArtifactCatalog>();
builder.Services.AddSingleton(new RmsDatabaseStorageOptions());
builder.Services.AddSingleton<IRmsDatabaseSqlOperations, RmsSqlDatabaseOperations>();
builder.Services.AddSingleton<IRmsDatabaseBackupStorage, RmsDatabaseBackupStorage>();
builder.Services.AddSingleton<IRmsDatabaseWorkflow, RmsDatabaseWorkflowService>();
builder.Services.AddSingleton<RmsDatabaseOperationStore>();
builder.Services.AddSingleton<RmsDatabaseIdempotencyStore>();
builder.Services.AddSingleton<RmsDatabaseConcurrencyGate>();
builder.Services.AddSingleton<IRmsPrivilegedAuditSink, InMemoryRmsAuditSink>();
builder.Services.AddSingleton<RmsDatabaseOperationRuntime>();

var app = builder.Build();

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
app.MapRmsDatabaseEndpoints();

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
