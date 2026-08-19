using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.CommandLine;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Configuration.Json;

namespace RmsSupportHub.Api.Configuration;

/// <summary>
/// Discovers, validates, and registers a server-owned external JSON configuration file
/// located outside the deployable application root.
///
/// Precedence contract (lower to higher priority):
/// packaged appsettings.json
/// &lt; packaged appsettings.{Environment}.json
/// &lt; server-owned external JSON
/// &lt; environment variables
/// &lt; command-line arguments
/// </summary>
public static class ExternalConfigurationLoader
{
    public const string ExternalConfigPathVariableName = "SUPPORTHUB_EXTERNAL_CONFIG_PATH";

    /// <summary>
    /// Validates and normalizes the raw configuration file path.
    /// Returns null if the path is null or empty (configuration absent).
    /// Throws InvalidOperationException if the path is invalid, non-local, a URL, a UNC path,
    /// or located inside the application content root.
    /// </summary>
    public static string? ValidateAndNormalizeExternalConfigPath(string? rawPath, string contentRootPath)
    {
        if (rawPath is null || rawPath.Length == 0)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(rawPath))
        {
            throw new InvalidOperationException("External configuration path cannot be whitespace.");
        }

        var trimmed = rawPath.Trim();

        // Reject explicit URL schemes and network URI structures
        if (trimmed.Contains("://", StringComparison.Ordinal) ||
            trimmed.StartsWith("http:", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("https:", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("ftp:", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("External configuration path must be a local file path and cannot be a URL.");
        }

        // Reject UNC paths and network shares (e.g. \\server\share or //server/share)
        if (trimmed.StartsWith(@"\\", StringComparison.Ordinal) ||
            trimmed.StartsWith("//", StringComparison.Ordinal) ||
            trimmed.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("External configuration path cannot be a UNC or network path.");
        }

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && (uri.IsUnc || !uri.IsFile))
        {
            throw new InvalidOperationException("External configuration path cannot be a UNC or non-file URI.");
        }

        // Reject relative paths; require absolute local path
        if (!Path.IsPathRooted(trimmed))
        {
            throw new InvalidOperationException("External configuration path must be an absolute path.");
        }

        if (OperatingSystem.IsWindows())
        {
            if (!Regex.IsMatch(trimmed, @"^[a-zA-Z]:[\\/]"))
            {
                throw new InvalidOperationException("External configuration path must be an absolute local path rooted with a drive letter.");
            }
        }
        else
        {
            if (!trimmed.StartsWith('/') || trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("External configuration path must be an absolute local filesystem path.");
            }
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(trimmed);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException("External configuration path is not a valid filesystem path.");
        }

        // Reject configuration located inside the deployed application content root
        if (!string.IsNullOrWhiteSpace(contentRootPath))
        {
            string fullContentRoot;
            try
            {
                fullContentRoot = Path.GetFullPath(contentRootPath);
            }
            catch
            {
                fullContentRoot = contentRootPath;
            }

            var contentRootWithSeparator = Path.TrimEndingDirectorySeparator(fullContentRoot) + Path.DirectorySeparatorChar;

            if (fullPath.Equals(fullContentRoot, StringComparison.OrdinalIgnoreCase) ||
                fullPath.StartsWith(contentRootWithSeparator, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("External configuration path cannot be located inside the application content root.");
            }
        }

        return fullPath;
    }

    /// <summary>
    /// Validates that the external configuration file exists, is readable, and contains a valid JSON object.
    /// Fails closed without disclosing sensitive file contents or secrets in error messages.
    /// </summary>
    public static void ValidateJsonFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"External configuration file was not found at '{filePath}'.");
        }

        try
        {
            using var stream = File.OpenRead(filePath);
            using var doc = JsonDocument.Parse(stream);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("External configuration root must be a JSON object.");
            }
        }
        catch (JsonException)
        {
            // Do not disclose file contents or tokens in exception message
            throw new InvalidOperationException("External configuration file contains invalid JSON.");
        }
        catch (UnauthorizedAccessException)
        {
            throw new InvalidOperationException("External configuration file could not be read due to permissions.");
        }
        catch (Exception ex) when (ex is not InvalidOperationException && ex is not FileNotFoundException)
        {
            throw new InvalidOperationException("External configuration file could not be read.");
        }
    }

    /// <summary>
    /// Discovers and applies the external server JSON configuration to the ConfigurationManager.
    /// Inserts the JSON source immediately before environment variables and command-line sources
    /// to guarantee the required precedence order.
    /// </summary>
    public static void Apply(ConfigurationManager configuration, string contentRootPath, string? explicitPath = null)
    {
        var rawPath = explicitPath ?? Environment.GetEnvironmentVariable(ExternalConfigPathVariableName);
        var normalizedPath = ValidateAndNormalizeExternalConfigPath(rawPath, contentRootPath);
        if (string.IsNullOrEmpty(normalizedPath))
        {
            return;
        }

        ValidateJsonFile(normalizedPath);

        var jsonSource = new JsonConfigurationSource
        {
            Path = normalizedPath,
            Optional = false,
            ReloadOnChange = false
        };
        jsonSource.ResolveFileProvider();

        // Insert after packaged JsonConfigurationSource instances (appsettings.json and appsettings.{Env}.json)
        // and before application-level environment variables / command-line arguments to ensure:
        // packaged appsettings < external JSON < environment variables < command line
        var lastJsonIndex = -1;
        for (var i = configuration.Sources.Count - 1; i >= 0; i--)
        {
            if (configuration.Sources[i] is JsonConfigurationSource)
            {
                lastJsonIndex = i;
                break;
            }
        }

        if (lastJsonIndex >= 0)
        {
            configuration.Sources.Insert(lastJsonIndex + 1, jsonSource);
        }
        else
        {
            var envOrCmdIndex = -1;
            for (var i = 0; i < configuration.Sources.Count; i++)
            {
                if (configuration.Sources[i] is EnvironmentVariablesConfigurationSource or CommandLineConfigurationSource)
                {
                    envOrCmdIndex = i;
                    break;
                }
            }

            if (envOrCmdIndex >= 0)
            {
                configuration.Sources.Insert(envOrCmdIndex, jsonSource);
            }
            else
            {
                configuration.Sources.Add(jsonSource);
            }
        }
    }

    /// <summary>
    /// WebApplicationBuilder extension method for chaining configuration bootstrap.
    /// </summary>
    public static WebApplicationBuilder AddExternalServerConfiguration(this WebApplicationBuilder builder, string? explicitPath = null)
    {
        Apply(builder.Configuration, builder.Environment.ContentRootPath, explicitPath);
        return builder;
    }
}
