using RmsSupportHub.Api.Exceptions;
using RmsSupportHub.Core.Models;
using Microsoft.Data.SqlClient;

namespace RmsSupportHub.Api;

/// <summary>
/// Single choke point for resolving named connection strings from configuration.
/// Real values come from .NET user-secrets in development or the
/// CONNECTIONSTRINGS__&lt;NAME&gt; environment variable in production — never from
/// a tracked appsettings*.json file. See README.md "Configuration &amp; secrets".
/// </summary>
public interface IConnectionStringResolver
{
    string RequireForEnvironment(ModuleEnvironment environment);
}

public sealed class ServerConnectionStringResolver : IConnectionStringResolver
{
    private readonly IConfiguration _configuration;

    public ServerConnectionStringResolver(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string RequireForEnvironment(ModuleEnvironment environment) =>
        ConnectionStringResolver.RequireForEnvironment(_configuration, environment);
}

public static class ConnectionStringResolver
{
    public static string Require(IConfiguration configuration, string name)
    {
        var value = configuration.GetConnectionString(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new EnvironmentUnconfiguredException();
        }
        return value;
    }

    /// <summary>Resolves the connection for one server-owned module
    /// environment. The environment supplies the named secret and, when
    /// required, an approved catalog override. No connection detail or catalog
    /// is accepted from the browser.</summary>
    public static string RequireForEnvironment(IConfiguration configuration, ModuleEnvironment environment)
    {
        var name = environment.ConnectionStringName;
        if (string.IsNullOrWhiteSpace(name))
            throw new EnvironmentUnconfiguredException();

        var connectionString = Require(configuration, name);
        if (!string.IsNullOrWhiteSpace(environment.DatabaseOverride))
        {
            var builder = new SqlConnectionStringBuilder(connectionString)
            {
                InitialCatalog = environment.DatabaseOverride
            };
            connectionString = builder.ConnectionString;
        }

        if (!connectionString.Contains("Connect Timeout", StringComparison.OrdinalIgnoreCase))
        {
            connectionString += ";Connect Timeout=5;";
        }

        return connectionString;
    }
}
