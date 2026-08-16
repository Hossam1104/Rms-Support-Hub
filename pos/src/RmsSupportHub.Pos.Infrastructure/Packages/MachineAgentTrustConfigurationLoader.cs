using System.Text.Json;
using RmsSupportHub.Pos.Domain.Models;
using RmsSupportHub.Pos.Infrastructure.Configuration;

namespace RmsSupportHub.Pos.Infrastructure.Packages;

public interface IAgentMachineTrustConfigurationLoader
{
    AgentMachineTrustConfiguration Load();

    bool TryLoad(out AgentMachineTrustConfiguration? configuration, out string failureCode);
}

/// <summary>
/// Loads and validates the machine-owned package trust configuration from the ACL-protected
/// control file. This loader enforces fixed paths, ownership, protected ACLs, bounded file size,
/// non-reparse constraints, strict JSON parsing, normalized distinct signer pins, and explicit
/// deploymentMode authority before producing an immutable trust snapshot.
/// </summary>
public sealed class MachineAgentTrustConfigurationLoader : IAgentMachineTrustConfigurationLoader
{
    public const int MaxControlFileBytes = 16 * 1024;

    public static readonly string CanonicalTrustConfigurationPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "DBS",
        "RmsSupportAgent",
        "Trust",
        "package-trust.json");

    private readonly string? testOnlyTrustConfigurationPath;

    public MachineAgentTrustConfigurationLoader()
    {
    }

    // This constructor is intentionally internal. It exists only so test DI can replace the
    // canonical source with a disposable fixture; normal Agent composition cannot select it from
    // IConfiguration, environment variables, command-line arguments, or package input.
    internal MachineAgentTrustConfigurationLoader(string testOnlyTrustConfigurationPath)
    {
        this.testOnlyTrustConfigurationPath = testOnlyTrustConfigurationPath;
    }

    public AgentMachineTrustConfiguration Load()
    {
        if (TryLoad(out var configuration, out var failureCode) && configuration is not null)
        {
            return configuration;
        }

        throw new InvalidOperationException($"The machine trust configuration could not be loaded: {failureCode}.");
    }

    // Test-only overloads. They are internal and are never part of the shipped Agent composition;
    // they let friend test assemblies exercise the same parser and ACL checks against disposable
    // fixtures without making an alternate path a normal runtime API.
    internal AgentMachineTrustConfiguration Load(string testOnlyPath) =>
        new MachineAgentTrustConfigurationLoader(testOnlyPath).Load();

    internal bool TryLoad(string testOnlyPath, out AgentMachineTrustConfiguration? configuration, out string failureCode) =>
        new MachineAgentTrustConfigurationLoader(testOnlyPath).TryLoad(out configuration, out failureCode);

    public bool TryLoad(out AgentMachineTrustConfiguration? configuration, out string failureCode)
    {
        configuration = null;
        failureCode = "unknown";

        try
        {
            var rawPath = testOnlyTrustConfigurationPath ?? CanonicalTrustConfigurationPath;
            if (string.IsNullOrWhiteSpace(rawPath)
                || !Path.IsPathFullyQualified(rawPath)
                || rawPath.Any(char.IsControl)
                || rawPath.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries).Any(segment => segment is "." or ".."))
            {
                failureCode = "machine_trust_path_invalid";
                return false;
            }

            var fullPath = Path.GetFullPath(rawPath);
            if (!File.Exists(fullPath))
            {
                failureCode = "machine_trust_file_missing";
                return false;
            }

            if (File.GetAttributes(fullPath).HasFlag(FileAttributes.ReparsePoint))
            {
                failureCode = "machine_trust_reparse_point";
                return false;
            }

            var fileInfo = new FileInfo(fullPath);
            if (fileInfo.Length is <= 0 or > MaxControlFileBytes)
            {
                failureCode = "machine_trust_file_size_invalid";
                return false;
            }

            // The trust configuration file is machine-owned and must pass the complete ACL and
            // ownership boundary before any value inside it is parsed or trusted.
            if (!ServiceOwnedDirectoryProvisioner.IsTrustedControlFile(fullPath))
            {
                failureCode = "machine_trust_untrusted_acl";
                return false;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(fullPath));
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                failureCode = "machine_trust_json_invalid";
                return false;
            }

            var root = document.RootElement;

            if (!root.TryGetProperty("productionSignerThumbprint", out var productionProp))
            {
                failureCode = "signer_thumbprint_missing";
                return false;
            }

            if (!root.TryGetProperty("testingSignerThumbprint", out var testingProp))
            {
                failureCode = "signer_thumbprint_missing";
                return false;
            }

            if (productionProp.ValueKind != JsonValueKind.String || testingProp.ValueKind != JsonValueKind.String)
            {
                failureCode = "signer_thumbprint_invalid";
                return false;
            }

            var production = AgentPackageTrustOptions.Normalize(productionProp.GetString());
            var testing = AgentPackageTrustOptions.Normalize(testingProp.GetString());
            if (production is null || testing is null)
            {
                failureCode = "signer_thumbprint_invalid";
                return false;
            }

            if (string.Equals(production, testing, StringComparison.Ordinal))
            {
                failureCode = "signer_pins_equal";
                return false;
            }

            if (!root.TryGetProperty("deploymentMode", out var modeProp))
            {
                failureCode = "machine_release_mode_missing";
                return false;
            }

            if (modeProp.ValueKind != JsonValueKind.String)
            {
                failureCode = "machine_release_mode_invalid";
                return false;
            }

            var mode = modeProp.GetString();
            if (mode is not (AgentProductIdentity.ReleaseChannelProduction or AgentProductIdentity.ReleaseChannelTesting))
            {
                failureCode = "machine_release_mode_invalid";
                return false;
            }

            configuration = new AgentMachineTrustConfiguration(
                mode,
                production,
                testing);
            failureCode = "machine_trust_configuration_valid";
            return true;
        }
        catch (JsonException)
        {
            failureCode = "machine_trust_json_invalid";
            return false;
        }
        catch (IOException)
        {
            failureCode = "machine_trust_io_error";
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            failureCode = "machine_trust_untrusted_acl";
            return false;
        }
        catch (Exception)
        {
            failureCode = "machine_trust_unknown_error";
            return false;
        }
    }
}
