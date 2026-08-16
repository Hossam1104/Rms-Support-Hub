using System.Globalization;
using System.Text;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Application.Packages;

/// <summary>
/// Creates the one deterministic payload signed by an Agent package publisher. The format is a
/// length-delimited UTF-8 field stream rather than serialized JSON, so property ordering, runtime
/// culture, and serializer changes cannot alter the meaning of a signature.
/// </summary>
public static class AgentPackageCanonicalizer
{
    public const string EnvelopeVersion = "RmsSupportAgent.PackageEnvelope.v1";

    public static byte[] Canonicalize(AgentPackageManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var builder = new StringBuilder();
        builder.Append(EnvelopeVersion).Append('\n');
        Append(builder, "schemaVersion", manifest.SchemaVersion.ToString(CultureInfo.InvariantCulture));
        Append(builder, "productId", manifest.ProductId ?? string.Empty);
        Append(builder, "packageId", manifest.PackageId);
        Append(builder, "version", manifest.Version);
        Append(builder, "supportedOperatingSystem", manifest.SupportedOperatingSystem);
        Append(builder, "supportedRuntime", manifest.SupportedRuntime);
        Append(builder, "serviceDisplayName", manifest.ServiceDisplayName);
        Append(builder, "serviceDescription", manifest.ServiceDescription ?? AgentProductIdentity.ServiceDescription);
        Append(builder, "serviceIdentity", manifest.ServiceIdentity);
        Append(builder, "scmName", manifest.ScmName);
        Append(builder, "signatureAlgorithm", manifest.SignatureAlgorithm);
        Append(builder, "signerDisplayName", manifest.SignerDisplayName);
        Append(builder, "architecture", manifest.Architecture ?? string.Empty);
        Append(builder, "releaseChannel", manifest.ReleaseChannel ?? string.Empty);
        Append(builder, "environment", manifest.Environment ?? string.Empty);
        Append(builder, "packageSha256", manifest.PackageSha256.ToLowerInvariant());
        Append(builder, "packageSizeBytes", manifest.PackageSizeBytes.ToString(CultureInfo.InvariantCulture));
        Append(builder, "previousVersion", manifest.PreviousVersion ?? string.Empty);
        Append(builder, "rollbackAvailable", manifest.RollbackAvailable ? "true" : "false");

        var files = (manifest.Files ?? [])
            .OrderBy(file => file.RelativePath.Replace('\\', '/'), StringComparer.Ordinal)
            .ThenBy(file => file.LogicalName, StringComparer.Ordinal)
            .ToArray();
        Append(builder, "files.count", files.Length.ToString(CultureInfo.InvariantCulture));
        for (var index = 0; index < files.Length; index++)
        {
            var file = files[index];
            var prefix = $"files[{index}]";
            Append(builder, prefix + ".logicalName", file.LogicalName);
            Append(builder, prefix + ".relativePath", file.RelativePath.Replace('\\', '/'));
            Append(builder, prefix + ".sizeBytes", file.SizeBytes.ToString(CultureInfo.InvariantCulture));
            Append(builder, prefix + ".sha256", file.Sha256.ToLowerInvariant());
            Append(builder, prefix + ".required", file.Required ? "true" : "false");
        }

        AppendList(builder, "aclRequirements", manifest.AclRequirements);
        AppendList(builder, "certificateRequirements", manifest.CertificateRequirements);
        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    public static string CanonicalizeText(AgentPackageManifest manifest) =>
        Encoding.UTF8.GetString(Canonicalize(manifest));

    private static void AppendList(StringBuilder builder, string name, IReadOnlyList<string>? values)
    {
        var ordered = (values ?? []).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        Append(builder, name + ".count", ordered.Length.ToString(CultureInfo.InvariantCulture));
        for (var index = 0; index < ordered.Length; index++)
        {
            Append(builder, $"{name}[{index}]", ordered[index]);
        }
    }

    private static void Append(StringBuilder builder, string name, string value)
    {
        var byteCount = Encoding.UTF8.GetByteCount(value);
        builder.Append(name)
            .Append('|')
            .Append(byteCount.ToString(CultureInfo.InvariantCulture))
            .Append(':')
            .Append(value)
            .Append('\n');
    }
}
