using System.Runtime.InteropServices;
using System.Security.Principal;

namespace RmsSupportHub.Pos.Agent.Authorization;

/// <summary>
/// Windows API adapter for account and local-group resolution. The caller remains responsible for
/// interpreting a failed lookup as authorization failure.
/// </summary>
public sealed class WindowsLocalGroupMembershipLookup : IWindowsLocalGroupMembershipLookup
{
    private const int Level = 0;
    private const int IncludeIndirect = 0x0001;
    private const int MaxPreferredLength = -1;
    private const int Success = 0;

    public bool TryResolveAccountName(SecurityIdentifier accountSid, out string accountName)
    {
        ArgumentNullException.ThrowIfNull(accountSid);

        accountName = string.Empty;
        try
        {
            if (accountSid.Translate(typeof(NTAccount)) is not NTAccount account
                || string.IsNullOrWhiteSpace(account.Value))
            {
                return false;
            }

            accountName = account.Value;
            return true;
        }
        catch (IdentityNotMappedException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public bool TryGetLocalGroupNames(string accountName, out IReadOnlyList<string> groupNames)
    {
        groupNames = [];
        if (string.IsNullOrWhiteSpace(accountName))
        {
            return false;
        }

        var buffer = IntPtr.Zero;
        try
        {
            var status = NetUserGetLocalGroups(
                serverName: null,
                userName: accountName,
                level: Level,
                flags: IncludeIndirect,
                bufferPointer: out buffer,
                preferredMaximumLength: MaxPreferredLength,
                entriesRead: out var entriesRead,
                totalEntries: out var totalEntries);

            if (status != Success
                || entriesRead < 0
                || totalEntries < entriesRead
                || (entriesRead > 0 && buffer == IntPtr.Zero))
            {
                return false;
            }

            var entrySize = Marshal.SizeOf<LocalGroupInfo0>();
            var names = new List<string>(entriesRead);
            for (var index = 0; index < entriesRead; index++)
            {
                var entryPointer = IntPtr.Add(buffer, index * entrySize);
                var entry = Marshal.PtrToStructure<LocalGroupInfo0>(entryPointer);
                var name = Marshal.PtrToStringUni(entry.Name);
                if (string.IsNullOrWhiteSpace(name))
                {
                    return false;
                }

                names.Add(name);
            }

            groupNames = names;
            return true;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
        catch (ExternalException)
        {
            return false;
        }
        finally
        {
            if (buffer != IntPtr.Zero)
            {
                _ = NetApiBufferFree(buffer);
            }
        }
    }

    public bool TryResolveSid(string accountName, out SecurityIdentifier sid)
    {
        ArgumentNullException.ThrowIfNull(accountName);

        sid = null!;
        if (string.IsNullOrWhiteSpace(accountName))
        {
            return false;
        }

        var candidateNames = accountName.Contains('\\', StringComparison.Ordinal)
            ? new[] { accountName }
            : new[] { accountName, $"{Environment.MachineName}\\{accountName}" };

        foreach (var candidateName in candidateNames)
        {
            try
            {
                if (new NTAccount(candidateName).Translate(typeof(SecurityIdentifier))
                    is SecurityIdentifier resolvedSid)
                {
                    sid = resolvedSid;
                    return true;
                }
            }
            catch (IdentityNotMappedException)
            {
                // Try the machine-qualified spelling when the API returns a bare local name.
            }
            catch (ArgumentException)
            {
                // Try the machine-qualified spelling when the API returns a bare local name.
            }
        }

        return false;
    }

    [DllImport("Netapi32.dll", CharSet = CharSet.Unicode, SetLastError = false)]
    private static extern int NetUserGetLocalGroups(
        string? serverName,
        string userName,
        int level,
        int flags,
        out IntPtr bufferPointer,
        int preferredMaximumLength,
        out int entriesRead,
        out int totalEntries);

    [DllImport("Netapi32.dll", ExactSpelling = true, SetLastError = false)]
    private static extern int NetApiBufferFree(IntPtr buffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct LocalGroupInfo0
    {
        public IntPtr Name;
    }
}
