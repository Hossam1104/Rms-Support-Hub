using OnlineOrderTool.Api.Exceptions;

namespace OnlineOrderTool.Api;

/// <summary>
/// Single choke point for resolving named connection strings from configuration.
/// Real values come from .NET user-secrets in development or the
/// CONNECTIONSTRINGS__&lt;NAME&gt; environment variable in production — never from
/// a tracked appsettings*.json file. See README.md "Configuration &amp; secrets".
/// </summary>
public static class ConnectionStringResolver
{
    public static string Require(IConfiguration configuration, string name)
    {
        var value = configuration.GetConnectionString(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ConfigurationException(
                $"ConnectionStrings:{name} is not configured. Set it via " +
                $"'dotnet user-secrets set ConnectionStrings:{name} \"...\"' in development, " +
                $"or the CONNECTIONSTRINGS__{name.ToUpperInvariant()} environment variable in production.");
        }
        return value;
    }
}
