using System.Text.Json;
using Microsoft.Data.SqlClient;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Infrastructure.Installation;

/// <summary>
/// Reads only the known RMS+ installation files. The parser deliberately selects individual
/// properties instead of deserializing arbitrary configuration so credentials and unrelated
/// integration secrets cannot enter the discovery model.
/// </summary>
public sealed class RmsInstallationDiscovery(RmsInstallationOptions options) :
    IRmsInstallationDiscovery,
    IRmsDatabaseConnectionStringSource
{
    public async Task<RmsInstallationSnapshot> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        var documents = await Task.WhenAll(
            ReadJsonAsync(options.RmsInfoPath, cancellationToken),
            ReadJsonAsync(options.BranchServerSettingsPath, cancellationToken),
            ReadJsonAsync(options.CashierServerSettingsPath, cancellationToken),
            ReadJsonAsync(options.CashierUiSettingsPath, cancellationToken),
            ReadJsonAsync(options.ServicesManagerSettingsPath, cancellationToken)).ConfigureAwait(false);

        var info = documents[0];
        var branch = documents[1];
        var cashier = documents[2];
        var cashierUi = documents[3];
        var servicesManager = documents[4];

        try
        {
            var rmsInfoBranchCode = GetString(info.Document, "BranchCode");
            var branchSettingsBranchCode = GetString(branch.Document, "BranchSettings", "BranchCode");
            var cashierBranchCode = GetString(cashier.Document, "PosBasicInfoSettings", "BranchCode");
            var uiBranchCode = GetString(cashierUi.Document, "GrpcServer", "BranchCode");

            var rmsInfoPosNumber = GetString(info.Document, "POSNumber");
            var cashierMachineNumber = GetString(cashier.Document, "PosBasicInfoSettings", "MachineNo");

            var rmsInfoMainBranchId = GetString(info.Document, "MainServerBranchId");
            var branchMainBranchId = GetString(branch.Document, "BranchSettings", "MainServerBranchId");
            var cashierMainBranchId = GetString(cashier.Document, "PosBasicInfoSettings", "MainServerBranchId");

            var rmsInfoMainPosId = GetString(info.Document, "MainServerPosId");
            var cashierMainPosId = GetString(cashier.Document, "PosBasicInfoSettings", "MainServerPosId");

            var branchBuild = GetString(branch.Document, "BuildNumber");
            var cashierBuild = GetString(cashier.Document, "BuildNumber");
            var cashierUiBuild = GetString(cashierUi.Document, "Settings", "BuildNumber");

            var mainServerUrl = FirstNonEmpty(
                GetString(info.Document, "MainServerUrl"),
                GetString(branch.Document, "BranchSettings", "MainBaseUrl"),
                GetString(cashier.Document, "PosBasicInfoSettings", "MainServerBaseUrl"));
            var branchServerAddress = FirstNonEmpty(
                GetString(info.Document, "BranchServerIP"),
                GetString(cashier.Document, "PosBasicInfoSettings", "BranchBaseUrl"));
            var branchEndpoint = ParseEndpoint(FirstNonEmpty(
                GetString(cashier.Document, "PosBasicInfoSettings", "BranchBaseUrl"),
                GetString(info.Document, "BranchServerIP")));
            var mainEndpoint = ParseEndpoint(mainServerUrl);

            var branchDatabase = ParseDatabaseConfiguration(
                branch,
                "BranchServer");
            var cashierDatabase = ParseDatabaseConfiguration(
                cashier,
                "RmsPos");

            var services = ReadServiceExpectations(servicesManager);
            var consistency = BuildConsistency(
                [rmsInfoBranchCode, branchSettingsBranchCode, cashierBranchCode, uiBranchCode],
                [rmsInfoPosNumber, cashierMachineNumber],
                [rmsInfoMainBranchId, branchMainBranchId, cashierMainBranchId],
                [rmsInfoMainPosId, cashierMainPosId],
                [branchBuild, cashierBuild, cashierUiBuild]);

            var branchInstalled = branch.Exists || Directory.Exists(options.BranchServerDirectory);
            var cashierInstalled = cashier.Exists
                || cashierUi.Exists
                || Directory.Exists(options.CashierServerDirectory)
                || Directory.Exists(options.CashierUiDirectory);
            var installationDetected = info.Exists
                || branchInstalled
                || cashierInstalled
                || servicesManager.Exists
                || Directory.Exists(options.ServicesManagerDirectory);

            return new(
                installationDetected,
                branchInstalled,
                cashierInstalled,
                FirstNonEmpty(rmsInfoBranchCode, branchSettingsBranchCode, cashierBranchCode, uiBranchCode),
                FirstNonEmpty(rmsInfoPosNumber, cashierMachineNumber),
                FirstNonEmpty(rmsInfoMainBranchId, branchMainBranchId, cashierMainBranchId),
                FirstNonEmpty(rmsInfoMainPosId, cashierMainPosId),
                FirstNonEmpty(
                    GetString(info.Document, "UninstallGUID"),
                    GetString(branch.Document, "BranchSettings", "InstallationGuid")),
                mainEndpoint.DisplayAddress,
                SafeAddressLabel(branchServerAddress),
                GetString(cashierUi.Document, "Settings", "TheClient"),
                FirstNonEmpty(cashierUiBuild, cashierBuild, branchBuild),
                new(branchBuild, cashierBuild, cashierUiBuild),
                consistency,
                branchDatabase,
                cashierDatabase,
                mainEndpoint,
                branchEndpoint,
                services);
        }
        finally
        {
            foreach (var source in documents)
            {
                source.Document?.Dispose();
            }
        }
    }

    public async Task<string?> GetConnectionStringAsync(
        RmsDatabaseKind database,
        CancellationToken cancellationToken = default)
    {
        var (path, section, key) = database switch
        {
            RmsDatabaseKind.Branch => (options.BranchServerSettingsPath, "ConnectionStrings", "BranchServer"),
            RmsDatabaseKind.Cashier => (options.CashierServerSettingsPath, "ConnectionStrings", "RmsPos"),
            _ => throw new ArgumentOutOfRangeException(nameof(database), database, "Unknown RMS database.")
        };

        var source = await ReadJsonAsync(path, cancellationToken).ConfigureAwait(false);
        try
        {
            return GetString(source.Document, section, key);
        }
        finally
        {
            source.Document?.Dispose();
        }
    }

    private static async Task<JsonSource> ReadJsonAsync(
        string path,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return new(false, false, null);
        }

        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            return new(true, true, JsonDocument.Parse(json, new JsonDocumentOptions
            {
                MaxDepth = 32,
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            }));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return new(true, false, null);
        }
    }

    private static RmsDatabaseConfiguration ParseDatabaseConfiguration(
        JsonSource source,
        string connectionStringKey)
    {
        if (!source.Valid)
        {
            return new(true, RmsConnectionStringState.Invalid, null, null, null);
        }

        var raw = GetString(source.Document, "ConnectionStrings", connectionStringKey);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new(false, RmsConnectionStringState.Unavailable, null, null, null);
        }

        try
        {
            var builder = new SqlConnectionStringBuilder(raw);
            return new(
                true,
                RmsConnectionStringState.Valid,
                SafeAddressLabel(builder.DataSource),
                SafeString(builder.InitialCatalog, 128),
                builder.IntegratedSecurity);
        }
        catch
        {
            return new(true, RmsConnectionStringState.Invalid, null, null, null);
        }
    }

    private RmsServiceExpectationsSnapshot ReadServiceExpectations(JsonSource source)
    {
        var configuredNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var services = GetProperty(source.Document?.RootElement, "Services");
        if (services is { ValueKind: JsonValueKind.Array })
        {
            foreach (var service in services.Value.EnumerateArray())
            {
                var serviceName = GetString(service, "ServiceName");
                if (serviceName is not null)
                {
                    configuredNames.Add(serviceName);
                }
            }
        }

        return new(
            File.Exists(options.ServicesManagerSettingsPath)
                || Directory.Exists(options.ServicesManagerDirectory),
            source.Exists && source.Valid,
            configuredNames.Contains(RmsServiceCatalog.BranchServiceName),
            configuredNames.Contains(RmsServiceCatalog.CashierServiceName));
    }

    private static RmsConsistencySnapshot BuildConsistency(
        IReadOnlyList<string?> branchCodes,
        IReadOnlyList<string?> posNumbers,
        IReadOnlyList<string?> mainBranchIds,
        IReadOnlyList<string?> mainPosIds,
        IReadOnlyList<string?> versions)
    {
        var states = new[]
        {
            EvaluateConsistency(branchCodes),
            EvaluateConsistency(posNumbers),
            EvaluateConsistency(mainBranchIds),
            EvaluateConsistency(mainPosIds),
            EvaluateConsistency(versions)
        };
        var warnings = new List<string>();
        if (states[0] == RmsConsistencyState.Mismatch)
        {
            warnings.Add("Branch code differs between installed RMS metadata files.");
        }

        if (states[1] == RmsConsistencyState.Mismatch)
        {
            warnings.Add("POS identity differs between installed RMS metadata files.");
        }

        if (states[2] == RmsConsistencyState.Mismatch)
        {
            warnings.Add("Main-server branch identity differs between installed RMS metadata files.");
        }

        if (states[3] == RmsConsistencyState.Mismatch)
        {
            warnings.Add("Main-server POS identity differs between installed RMS metadata files.");
        }

        if (states[4] == RmsConsistencyState.Mismatch)
        {
            warnings.Add("Installed RMS component versions do not match.");
        }

        return new(states[0], states[1], states[2], states[3], states[4], warnings);
    }

    private static RmsConsistencyState EvaluateConsistency(IReadOnlyList<string?> values)
    {
        var present = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .ToArray();
        return present.Length switch
        {
            0 => RmsConsistencyState.Unavailable,
            1 => RmsConsistencyState.Consistent,
            _ when present.All(value => string.Equals(value, present[0], StringComparison.OrdinalIgnoreCase))
                => RmsConsistencyState.Consistent,
            _ => RmsConsistencyState.Mismatch
        };
    }

    private static RmsEndpointConfiguration ParseEndpoint(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new(RmsEndpointConfigurationState.Unavailable, null, null, null);
        }

        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
            || uri.Host.Length == 0
            || uri.UserInfo.Length > 0
            || (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            return new(RmsEndpointConfigurationState.Invalid, null, null, null);
        }

        var port = uri.IsDefaultPort
            ? string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ? 443 : 80
            : uri.Port;
        var host = SafeString(uri.Host, 255);
        if (host is null || port is < 1 or > 65535)
        {
            return new(RmsEndpointConfigurationState.Invalid, null, null, null);
        }

        return new(RmsEndpointConfigurationState.Configured, host, port, FormatAddress(host, port));
    }

    private static string? SafeAddressLabel(string? value)
    {
        var safe = SafeString(value, 512);
        if (safe is null)
        {
            return null;
        }

        if (Uri.TryCreate(safe, UriKind.Absolute, out var uri))
        {
            if (uri.Host.Length == 0 || uri.UserInfo.Length > 0)
            {
                return null;
            }

            var port = uri.IsDefaultPort
                ? string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ? 443 : 80
                : uri.Port;
            var host = SafeString(uri.Host, 255);
            return host is not null && port is >= 1 and <= 65535
                ? FormatAddress(host, port)
                : null;
        }

        if (!IsSafeAddressToken(safe))
        {
            return null;
        }

        return safe.TrimEnd('/');
    }

    private static bool IsSafeAddressToken(string value) =>
        value.All(character =>
            char.IsLetterOrDigit(character)
            || character is '.' or ':' or ',' or '\\' or '[' or ']' or '-' or '_' or '(' or ')');

    private static string FormatAddress(string host, int port) =>
        host.Contains(':') && !host.StartsWith("[", StringComparison.Ordinal)
            ? $"[{host}]:{port}"
            : $"{host}:{port}";

    private static string? GetString(JsonDocument? document, params string[] path) =>
        GetString(document?.RootElement, path);

    private static string? GetString(JsonElement? element, params string[] path)
    {
        var current = element;
        foreach (var segment in path)
        {
            var property = GetProperty(current, segment);
            if (property is null)
            {
                return null;
            }

            current = property.Value;
        }

        if (current is not { } currentValue)
        {
            return null;
        }

        return currentValue.ValueKind switch
        {
            JsonValueKind.String => SafeString(currentValue.GetString(), 1024),
            JsonValueKind.Number => SafeString(currentValue.GetRawText(), 128),
            _ => null
        };
    }

    private static JsonElement? GetProperty(JsonElement? element, string name)
    {
        if (element is not { ValueKind: JsonValueKind.Object } objectElement)
        {
            return null;
        }

        foreach (var property in objectElement.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }

        return null;
    }

    private static string? SafeString(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength && trimmed.All(character => !char.IsControl(character))
            ? trimmed
            : null;
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private sealed record JsonSource(bool Exists, bool Valid, JsonDocument? Document);
}
