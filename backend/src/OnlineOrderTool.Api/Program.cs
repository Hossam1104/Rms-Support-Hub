using System.Text.Json;
using System.Text.Json.Serialization;
using OnlineOrderTool.Api.Middleware;
using OnlineOrderTool.Core.Modules;
using OnlineOrderTool.Core.Repositories;
using OnlineOrderTool.Core.Services;
using OnlineOrderTool.Data;
using OnlineOrderTool.Data.Repositories;

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
builder.Services.AddSingleton<IDraftManager, DraftManager>();
builder.Services.AddSingleton<IFlatOrderPayloadBuilder, FlatOrderPayloadBuilder>();
builder.Services.AddSingleton<IUniCommercePayloadBuilder, UniCommercePayloadBuilder>();
builder.Services.AddSingleton<IFlatOrderValidator, FlatOrderValidator>();
builder.Services.AddSingleton<IUniCommerceValidator, UniCommerceValidator>();

builder.Services.AddSingleton<ISqlServerConnectionFactory, SqlServerConnectionFactory>();
builder.Services.AddSingleton<IGhcItemRepository, FlatOrderItemRepository>();
builder.Services.AddSingleton<IUpcItemRepository, UpcItemRepository>();
builder.Services.AddSingleton<IGhcConsumerRepository, GhcConsumerRepository>();
builder.Services.AddSingleton<IUpcConsumerRepository, UpcConsumerRepository>();
builder.Services.AddSingleton<IOrderRequestRepository, OrderRequestRepository>();

builder.Services.AddHttpClient<IApiClient, ApiClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
});

var app = builder.Build();

app.UseMiddleware<ExceptionMiddleware>();

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
