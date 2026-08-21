using RmsSupportHub.Core.Models;

namespace RmsSupportHub.Api.Configuration;

/// <summary>Resolves the one bounded downstream credential supported by the
/// current module contract. The browser only supplies module/environment keys;
/// the value is read from server-owned configuration immediately before the
/// outbound request and is never copied into a DTO or log message.</summary>
public interface IOutboundApiKeyResolver
{
    string? Resolve(ModuleEnvironment environment);
}

public sealed class ServerOutboundApiKeyResolver : IOutboundApiKeyResolver
{
    private readonly IConfiguration _configuration;

    public ServerOutboundApiKeyResolver(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string? Resolve(ModuleEnvironment environment) =>
        !environment.RequiresApiKey || string.IsNullOrWhiteSpace(environment.ApiKeyConfigurationKey)
            ? null
            : _configuration[$"ModuleApiKeys:{environment.ApiKeyConfigurationKey}"];
}

public sealed class EmptyOutboundApiKeyResolver : IOutboundApiKeyResolver
{
    public string? Resolve(ModuleEnvironment environment) => null;
}
