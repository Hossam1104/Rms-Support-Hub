using System.Security.AccessControl;
using System.Security.Principal;
using System.IO.Pipes;

namespace RmsSupportHub.Pos.LocalIpc;

public interface ILocalIpcOperatorGroupResolver
{
    bool TryResolve(string configuredGroupName, out SecurityIdentifier operatorGroupSid);
}

/// <summary>
/// Resolves only the explicitly configured local operator group. There is intentionally no broad
/// principal fallback when resolution fails.
/// </summary>
public sealed class WindowsLocalIpcOperatorGroupResolver : ILocalIpcOperatorGroupResolver
{
    public bool TryResolve(string configuredGroupName, out SecurityIdentifier operatorGroupSid)
    {
        operatorGroupSid = null!;
        if (string.IsNullOrWhiteSpace(configuredGroupName)
            || configuredGroupName.Length > 256
            || configuredGroupName.Any(char.IsControl))
        {
            return false;
        }

        var candidates = configuredGroupName.Contains('\\', StringComparison.Ordinal)
            ? new[] { configuredGroupName }
            : new[] { configuredGroupName, $"{Environment.MachineName}\\{configuredGroupName}" };

        foreach (var candidate in candidates)
        {
            try
            {
                if (new NTAccount(candidate).Translate(typeof(SecurityIdentifier))
                    is SecurityIdentifier resolved)
                {
                    operatorGroupSid = resolved;
                    return true;
                }
            }
            catch (IdentityNotMappedException)
            {
                // Try the machine-qualified spelling when the bare local name is not resolvable.
            }
            catch (ArgumentException)
            {
                // Malformed or unavailable identities fail closed.
            }
        }

        return false;
    }
}

/// <summary>
/// Builds the exact allow-list ACL for the local IPC endpoint. Protection is explicit and no
/// inherited or broad principals are retained.
/// </summary>
public static class LocalIpcSecurityDescriptor
{
    public static PipeSecurity Create(SecurityIdentifier operatorGroupSid)
    {
        ArgumentNullException.ThrowIfNull(operatorGroupSid);

        var security = new PipeSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        AddAllow(security, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, domainSid: null));
        AddAllow(security, new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, domainSid: null));
        AddAllow(security, operatorGroupSid);
        return security;
    }

    private static void AddAllow(PipeSecurity security, SecurityIdentifier sid) =>
        security.AddAccessRule(new PipeAccessRule(
            sid,
            // Named pipe clients need the full pipe handle rights negotiated by CreateFile. The
            // boundary is the explicit principal allow-list; no broad identity is ever granted.
            PipeAccessRights.FullControl,
            AccessControlType.Allow));
}
