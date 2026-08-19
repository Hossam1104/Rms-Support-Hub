using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using RmsSupportHub.Api.Configuration;
using RmsSupportHub.Api.Middleware;
using RmsSupportHub.Api;
using RmsSupportHub.Core.Modules;
using RmsSupportHub.Core.Repositories;
using RmsSupportHub.Core.Services;
using RmsSupportHub.Data;
using RmsSupportHub.Data.Repositories;

var builder = WebApplication.CreateBuilder(args);
builder.AddExternalServerConfiguration();

// Add Controllers with JSON camelCase formatting
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// Configure CORS for Angular frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularClient", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddOpenApi();

// Bind and validate the one server-owned environment contract. The request
// pipeline receives the resulting typed snapshot; no controller or Core
// service reads arbitrary IConfiguration values.
builder.Services.AddOptions<SupportHubOptions>()
    .Bind(builder.Configuration.GetSection(SupportHubOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<SupportHubOptions>>(
    new SupportHubOptionsValidator(builder.Configuration));

builder.Services.AddSingleton<IEnvironmentPolicy>(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<SupportHubOptions>>().Value;
    if (!DeploymentTierParser.TryParseExact(options.DeploymentTier, out var deploymentTier))
        throw new InvalidOperationException("SupportHub:DeploymentTier must be Testing or Production.");
    return new EnvironmentPolicy(deploymentTier);
});

// Register Core & Data Services. The API composition root is the only place
// that maps configuration onto module environment registrations.
builder.Services.AddSingleton<IModuleRegistry>(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<SupportHubOptions>>().Value;
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    return new ModuleRegistry(
        serviceProvider.GetRequiredService<IFlatOrderPayloadBuilder>(),
        serviceProvider.GetRequiredService<IFlatOrderValidator>(),
        serviceProvider.GetRequiredService<IUniCommercePayloadBuilder>(),
        serviceProvider.GetRequiredService<IUniCommerceValidator>(),
        serviceProvider.GetRequiredService<IGhcItemRepository>(),
        serviceProvider.GetRequiredService<IGhcConsumerRepository>(),
        serviceProvider.GetRequiredService<IUpcItemRepository>(),
        serviceProvider.GetRequiredService<IUpcConsumerRepository>(),
        ConfiguredEnvironmentCatalog.Build(configuration, options));
});
builder.Services.AddSingleton<IConnectionStringResolver, ServerConnectionStringResolver>();

// Drafts live under <content root>/var/drafts/, not the process's working
// directory (see remediation_plan.md B20), keyed by (sessionId, moduleKey)
// via SessionIdMiddleware (see B19).
var draftsRoot = Path.Combine(builder.Environment.ContentRootPath, "var", "drafts");
builder.Services.AddSingleton<IDraftManager>(_ => new DraftManager(draftsRoot));

builder.Services.AddSingleton<IFlatOrderPayloadBuilder, FlatOrderPayloadBuilder>();
builder.Services.AddSingleton<IUniCommercePayloadBuilder, UniCommercePayloadBuilder>();
builder.Services.AddSingleton<IFlatOrderValidator, FlatOrderValidator>();
builder.Services.AddSingleton<IUniCommerceValidator, UniCommerceValidator>();

builder.Services.AddSingleton<ISqlServerConnectionFactory, SqlServerConnectionFactory>();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IGhcItemRepository, FlatOrderItemRepository>();
builder.Services.AddSingleton<IUpcItemRepository, UpcItemRepository>();
builder.Services.AddSingleton<IGhcConsumerRepository, GhcConsumerRepository>();
builder.Services.AddSingleton<IUpcConsumerRepository, UpcConsumerRepository>();
builder.Services.AddSingleton<IBranchRepository, BranchRepository>();
builder.Services.AddSingleton<IOrderRequestRepository, OrderRequestRepository>();

// Outbound TLS certificate validation is bypassed by default because the
// internal RMS hosts (10.10.x.x) present self-signed certificates -- but
// that bypass is now an explicit, logged config decision (Outbound:VerifyTls)
// rather than an unconditional `=> true` with no opt-out (see
// remediation_plan.md B17).
var verifyTls = builder.Configuration.GetValue("Outbound:VerifyTls", defaultValue: false);

builder.Services.AddHttpClient<IApiClient, ApiClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    ServerCertificateCustomValidationCallback = verifyTls
        ? null
        : (sender, cert, chain, sslPolicyErrors) => true
});

// The probe sweep is cached process-wide (singleton) while the service itself
// stays transient alongside the typed IApiClient it probes with.
builder.Services.AddSingleton<ModuleHealthCache>();
builder.Services.AddTransient<IModuleHealthService, ModuleHealthService>();

var app = builder.Build();

if (!verifyTls)
{
    app.Logger.LogWarning(
        "Outbound TLS certificate validation is DISABLED (Outbound:VerifyTls=false). " +
        "This bypasses certificate checks for all outbound HTTP calls made by IApiClient -- " +
        "intended only for the self-signed internal RMS hosts. Set Outbound:VerifyTls=true to enable validation.");
}

app.UseMiddleware<ExceptionMiddleware>();
app.UseMiddleware<SessionIdMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("AngularClient");
app.UseHttpsRedirection();

// Serve the Angular production build from wwwroot; UseDefaultFiles resolves
// "/" to wwwroot/index.html before UseStaticFiles serves the matched file.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthorization();
app.MapControllers();

// SPA fallback: any GET that isn't a static file and doesn't match an API
// controller route (deep links, browser refresh) falls through to
// index.html so Angular's router takes over client-side.
// Mistyped or retired /api/** paths return 404 across all HTTP methods (GET,
// POST, PUT, DELETE, PATCH, etc.) rather than returning 405 or 200 HTML.
app.MapFallback("{*path:nonfile}", async context =>
{
    if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    var fileInfo = app.Environment.WebRootFileProvider.GetFileInfo("index.html");
    if (fileInfo.Exists)
    {
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.SendFileAsync(fileInfo);
    }
    else
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
    }
});

app.Run();

public partial class Program { }
