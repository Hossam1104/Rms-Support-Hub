using System.Text.Json;
using System.Text.Json.Serialization;
using RmsSupportHub.Api.Middleware;
using RmsSupportHub.Core.Modules;
using RmsSupportHub.Core.Repositories;
using RmsSupportHub.Core.Services;
using RmsSupportHub.Data;
using RmsSupportHub.Data.Repositories;

var builder = WebApplication.CreateBuilder(args);

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

// Register Core & Data Services
builder.Services.AddSingleton<IModuleRegistry, ModuleRegistry>();

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
app.UseStaticFiles();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program { }
