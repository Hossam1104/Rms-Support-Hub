using System.Net;
using System.Runtime.InteropServices;

namespace RmsSupportHub.Pos.Infrastructure.Smb;

public enum SmbConnectionOutcome
{
    NoCredentialRequired,
    EstablishedByScope,
    CompatiblePreExisting,
    CredentialConflict,
    Failed
}

/// <summary>Small native seam so ownership and cleanup semantics are testable without an SMB share.</summary>
public interface ISmbConnectionApi
{
    int Add(string shareRoot, string username, string password);

    int Cancel(string shareRoot, bool force);
}

/// <summary>
/// Owns only a connection established by this scope. A pre-existing compatible connection is
/// observed but never disconnected by this instance.
/// </summary>
public sealed class SmbConnectionScope : IDisposable
{
    private readonly ISmbConnectionApi _api;
    private readonly string? _connectedRoot;
    private readonly bool _ownsConnection;
    private int _disposed;

    private SmbConnectionScope(
        ISmbConnectionApi api,
        string? connectedRoot,
        bool ownsConnection,
        SmbConnectionOutcome outcome)
    {
        _api = api;
        _connectedRoot = connectedRoot;
        _ownsConnection = ownsConnection;
        Outcome = outcome;
    }

    public SmbConnectionOutcome Outcome { get; }

    public bool OwnsConnection => _ownsConnection;

    public static SmbConnectionScope Connect(
        string shareRoot,
        string username,
        string password,
        ISmbConnectionApi? api = null)
    {
        ValidateShareRoot(shareRoot);
        api ??= WindowsSmbConnectionApi.Instance;

        if (string.IsNullOrWhiteSpace(username))
        {
            return new SmbConnectionScope(api, null, ownsConnection: false, SmbConnectionOutcome.NoCredentialRequired);
        }

        var result = api.Add(shareRoot, username, password);
        return result switch
        {
            NoError => new SmbConnectionScope(api, shareRoot, ownsConnection: true, SmbConnectionOutcome.EstablishedByScope),
            ErrorAlreadyAssigned => new SmbConnectionScope(api, null, ownsConnection: false, SmbConnectionOutcome.CompatiblePreExisting),
            ErrorSessionCredentialConflict => throw new SmbConnectionException("downloader.smb_credential_conflict"),
            _ => throw new SmbConnectionException("downloader.smb_connection_failed")
        };
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0 || !_ownsConnection || _connectedRoot is null)
        {
            return;
        }

        // Cleanup is best-effort and never force-disconnects a mapping that may have been reused by
        // another operation. The scope is the only owner of this successful Add call.
        _ = _api.Cancel(_connectedRoot, force: false);
    }

    private static void ValidateShareRoot(string shareRoot)
    {
        if (string.IsNullOrWhiteSpace(shareRoot)
            || !shareRoot.StartsWith("\\\\", StringComparison.Ordinal)
            || shareRoot.Count(character => character == '\\') != 3
            || shareRoot.Any(char.IsControl)
            || shareRoot.Contains('%', StringComparison.Ordinal)
            || shareRoot.Contains("..", StringComparison.Ordinal))
        {
            throw new SmbConnectionException("downloader.smb_root_rejected");
        }

        var root = shareRoot[2..];
        var separator = root.IndexOf('\\');
        if (separator <= 0 || separator == root.Length - 1)
        {
            throw new SmbConnectionException("downloader.smb_root_rejected");
        }

        var host = root[..separator];
        var share = root[(separator + 1)..];
        var validHost = host.StartsWith("[", StringComparison.Ordinal)
            ? host.EndsWith("]", StringComparison.Ordinal)
                && IPAddress.TryParse(host[1..^1], out var parsed)
                && parsed.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
            : host.All(character => char.IsLetterOrDigit(character) || character is '.' or '-');
        if (!validHost
            || share.Length > 80
            || share.Any(char.IsWhiteSpace)
            || share.Any(character => !char.IsLetterOrDigit(character) && character is not ('-' or '_' or '$' or '.')))
        {
            throw new SmbConnectionException("downloader.smb_root_rejected");
        }
    }

    private const int ResourceTypeDisk = 0x00000001;
    private const int NoError = 0;
    private const int ErrorAlreadyAssigned = 85;
    private const int ErrorSessionCredentialConflict = 1219;

    private sealed class WindowsSmbConnectionApi : ISmbConnectionApi
    {
        public static WindowsSmbConnectionApi Instance { get; } = new();

        public int Add(string shareRoot, string username, string password)
        {
            var netResource = new NetResource
            {
                dwType = ResourceTypeDisk,
                lpRemoteName = shareRoot
            };
            return WNetAddConnection2(ref netResource, password, username, 0);
        }

        public int Cancel(string shareRoot, bool force) => WNetCancelConnection2(shareRoot, 0, force);

        [StructLayout(LayoutKind.Sequential)]
        private struct NetResource
        {
            public int dwScope;
            public int dwType;
            public int dwDisplayType;
            public int dwUsage;
            public string? lpLocalName;
            public string? lpRemoteName;
            public string? lpComment;
            public string? lpProvider;
        }

        [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
        private static extern int WNetAddConnection2(ref NetResource netResource, string? password, string? username, int flags);

        [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
        private static extern int WNetCancelConnection2(string name, int flags, bool force);
    }
}

public sealed class SmbConnectionException(string code) : InvalidOperationException("The SMB connection could not be established.")
{
    public string Code { get; } = code;
}
