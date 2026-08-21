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

    public string? Resolve(ModuleEnvironment environment)
    {
        if (!environment.RequiresApiKey || string.IsNullOrWhiteSpace(environment.ApiKeyConfigurationKey))
            return null;

        var value = _configuration[$"ModuleApiKeys:{environment.ApiKeyConfigurationKey}"];
        return OutboundApiKeyValidation.IsSafeHeaderValue(value) ? value : null;
    }
}

internal static class OutboundApiKeyValidation
{
    public static bool IsSafeHeaderValue(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.IndexOf('\r') < 0
        && value.IndexOf('\n') < 0;
}

public sealed class EmptyOutboundApiKeyResolver : IOutboundApiKeyResolver
{
    public string? Resolve(ModuleEnvironment environment) => null;
}
