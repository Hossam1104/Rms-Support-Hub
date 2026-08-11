using System.Net;

namespace RmsSupportHub.Pos.Infrastructure.Smb;

/// <summary>
/// Converts server-local drive paths to UNC paths only inside the server-owned configured root.
/// Browser input is never accepted by this class.
/// </summary>
public static class SmbPathResolver
{
    public static string ToUncPath(string serverIp, string remoteLocalPath) =>
        ToUncPath(serverIp, remoteLocalPath, remoteLocalPath);

    public static string ToUncPath(string serverIp, string remoteLocalPath, string approvedRootFolder)
    {
        var server = ValidateServerAddress(serverIp);
        var root = ValidateCanonicalRoot(approvedRootFolder);
        var candidate = ValidateDrivePath(remoteLocalPath);
        if (!IsWithinRoot(candidate, root))
        {
            throw new SmbPathPolicyException("downloader.smb_root_rejected");
        }

        var drive = char.ToUpperInvariant(candidate[0]);
        var driveRoot = Path.GetPathRoot(candidate) ?? $"{drive}:\\";
        var relative = Path.GetRelativePath(driveRoot, candidate);
        var share = $"{drive}$";
        var host = server.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
            ? $"[{server}]"
            : server.ToString();
        return string.Equals(relative, ".", StringComparison.Ordinal)
            ? $@"\\{host}\{share}"
            : $@"\\{host}\{share}\{relative.Replace('/', '\\')}";
    }

    public static string ToUncShareRoot(string serverIp, string approvedRootFolder)
    {
        var server = ValidateServerAddress(serverIp);
        var root = ValidateCanonicalRoot(approvedRootFolder);
        var drive = char.ToUpperInvariant(root[0]);
        var host = server.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
            ? $"[{server}]"
            : server.ToString();
        return $@"\\{host}\{drive}$";
    }

    public static string ValidateCanonicalRoot(string? rootFolder)
    {
        if (string.IsNullOrWhiteSpace(rootFolder))
        {
            throw new SmbPathPolicyException("downloader.smb_root_rejected");
        }

        var candidate = rootFolder.Trim().Replace('/', '\\');
        if (candidate.Contains('%', StringComparison.Ordinal)
            || candidate.StartsWith("\\\\", StringComparison.Ordinal)
            || candidate.StartsWith("\\?\\", StringComparison.Ordinal)
            || candidate.StartsWith("\\.\\", StringComparison.Ordinal)
            || candidate.StartsWith("\\??\\", StringComparison.Ordinal)
            || candidate.Length < 3
            || !char.IsLetter(candidate[0])
            || candidate[1] != ':'
            || candidate[2] != '\\'
            || candidate.Any(char.IsControl))
        {
            throw new SmbPathPolicyException("downloader.smb_root_rejected");
        }

        if (candidate.Split('\\', StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => segment is "." or ".."))
        {
            throw new SmbPathPolicyException("downloader.smb_root_rejected");
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(candidate);
        }
        catch
        {
            throw new SmbPathPolicyException("downloader.smb_root_rejected");
        }

        if (fullPath.Length < 3
            || fullPath[1] != ':'
            || fullPath[2] != '\\'
            || !string.Equals(Path.GetPathRoot(fullPath), fullPath[..3], StringComparison.OrdinalIgnoreCase))
        {
            throw new SmbPathPolicyException("downloader.smb_root_rejected");
        }

        return TrimTrailingSeparators(fullPath);
    }

    public static string ValidateDrivePath(string? remoteLocalPath)
    {
        if (string.IsNullOrWhiteSpace(remoteLocalPath))
        {
            throw new SmbPathPolicyException("downloader.smb_root_rejected");
        }

        var candidate = remoteLocalPath.Trim().Replace('/', '\\');
        if (candidate.Length < 3
            || !char.IsLetter(candidate[0])
            || candidate[1] != ':'
            || candidate[2] != '\\'
            || candidate.Contains('%', StringComparison.Ordinal)
            || candidate.Any(char.IsControl)
            || candidate.StartsWith("\\\\", StringComparison.Ordinal)
            || candidate.StartsWith("\\?\\", StringComparison.Ordinal)
            || candidate.StartsWith("\\.\\", StringComparison.Ordinal))
        {
            throw new SmbPathPolicyException("downloader.smb_root_rejected");
        }

        if (candidate.Split('\\', StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => segment is "." or ".."))
        {
            throw new SmbPathPolicyException("downloader.smb_root_rejected");
        }

        try
        {
            return TrimTrailingSeparators(Path.GetFullPath(candidate));
        }
        catch
        {
            throw new SmbPathPolicyException("downloader.smb_root_rejected");
        }
    }

    public static string ValidateRemoteFilePath(string remoteFilePath, string approvedRootFolder)
    {
        var root = ValidateCanonicalRoot(approvedRootFolder);
        var file = ValidateDrivePath(remoteFilePath);
        if (!IsWithinRoot(file, root)
            || !ValidateSafeFileName(Path.GetFileName(file), requireZip: true).Equals(Path.GetFileName(file), StringComparison.Ordinal))
        {
            throw new SmbPathPolicyException("downloader.smb_root_rejected");
        }

        return file;
    }

    public static string ValidateSafeFileName(string? fileName, bool requireZip = false)
    {
        if (string.IsNullOrWhiteSpace(fileName)
            || fileName is "." or ".."
            || fileName.Length > 255
            || fileName.Any(char.IsControl)
            || fileName.Contains('/', StringComparison.Ordinal)
            || fileName.Contains('\\', StringComparison.Ordinal)
            || fileName.Contains(':', StringComparison.Ordinal)
            || Path.GetInvalidFileNameChars().Any(fileName.Contains))
        {
            throw new SmbPathPolicyException("downloader.smb_root_rejected");
        }

        if (requireZip && !fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            throw new SmbPathPolicyException("downloader.smb_root_rejected");
        }

        return fileName;
    }

    public static bool IsWithinRoot(string candidate, string root)
    {
        var normalizedCandidate = TrimTrailingSeparators(candidate);
        var normalizedRoot = TrimTrailingSeparators(root);
        return string.Equals(normalizedCandidate, normalizedRoot, StringComparison.OrdinalIgnoreCase)
            || normalizedCandidate.StartsWith(normalizedRoot + "\\", StringComparison.OrdinalIgnoreCase);
    }

    public static IPAddress ValidateServerAddress(string? serverIp)
    {
        if (!IPAddress.TryParse(serverIp, out var parsed))
        {
            throw new SmbPathPolicyException("downloader.smb_root_rejected");
        }

        var address = parsed.IsIPv4MappedToIPv6 ? parsed.MapToIPv4() : parsed;
        var bytes = address.GetAddressBytes();
        if (IPAddress.IsLoopback(address)
            || address.Equals(IPAddress.Any)
            || address.Equals(IPAddress.IPv6Any)
            || address.IsIPv6LinkLocal
            || address.IsIPv6Multicast
            || address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                && bytes is [169, 254, _, _])
        {
            throw new SmbPathPolicyException("downloader.smb_root_rejected");
        }

        return address;
    }

    private static string TrimTrailingSeparators(string path)
    {
        if (path.Length <= 3) return path;
        return path.TrimEnd('\\');
    }
}

public sealed class SmbPathPolicyException(string code) : InvalidOperationException("The configured SMB scope was rejected.")
{
    public string Code { get; } = code;
}
