using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;
using RmsSupportHub.Pos.Agent;
using RmsSupportHub.Pos.Agent.Artifacts;
using RmsSupportHub.Pos.Agent.Authorization;
using RmsSupportHub.Pos.Agent.Correlation;
using RmsSupportHub.Pos.Agent.MutationTokens;
using RmsSupportHub.Pos.Agent.Security;
using RmsSupportHub.Pos.Contracts.V1.Session;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Infrastructure.Backups;
using RmsSupportHub.Pos.Infrastructure.Configuration;

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

// INT-04 composes only the safe storage ports. It deliberately does not register legacy WinUI
// configuration importers, configuration mutation services, operation workers, or feature endpoints.
builder.Services.AddSingleton(new AgentConfigurationStoreOptions());
builder.Services.AddSingleton<IAgentConfigurationStore, JsonAgentConfigurationStore>();
builder.Services.AddSingleton<IAgentSecretStore, DpapiAgentSecretStore>();

// ArtifactCatalog is retained as a process-local foundation, but no HTTP artifact endpoint is
// mapped in this session. Its file capability remains behind the existing Infrastructure port.
builder.Services.AddSingleton<IBackupFileSystem, PhysicalBackupFileSystem>();
builder.Services.AddSingleton<ArtifactCatalog>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<CanonicalHostValidationMiddleware>();
app.UseMiddleware<HttpsOnlyMiddleware>();
app.UseRouting();

// CORS must run before authentication so a valid exact-origin OPTIONS request remains anonymous.
app.UseMiddleware<CorsPreflightValidationMiddleware>();
app.UseCors(AgentCors.PolicyName);
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<ExactOriginMiddleware>();

app.MapGet("/health/live", () => Results.Ok(new { status = "live" }))
    .AllowAnonymous()
    .WithName("GetHealthLive");
app.MapGet("/health/ready", () => Results.Ok(new { status = "ready" }))
    .AllowAnonymous()
    .WithName("GetHealthReady");

app.MapGet("/api/v1/session", (HttpContext context, IAdministratorGroupChecker administratorGroupChecker) =>
    {
        if (!AgentPrincipal.TryGetSid(context.User, out _))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "The authenticated Windows SID could not be resolved.");
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
    .WithName("GetAgentSession");

app.Run();

public partial class Program;
