using System.Text.Json;
using System.Text.Json.Serialization;
using RmsSupportHub.Pos.Domain.Models;
using RmsSupportHub.Pos.Infrastructure.Configuration;

namespace RmsSupportHub.Pos.Infrastructure.Packages;

/// <summary>Atomic durable lifecycle checkpoints under the fixed package root.</summary>
public sealed class AgentPackageLifecycleStateStore(AgentPackageOptions options)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly object gate = new();

    public AgentPackageLifecycleState? Read()
    {
        options.EnsureStorageProvisioned();
        lock (gate)
        {
            try
            {
                var path = options.LifecycleStatePath;
                if (!File.Exists(path))
                {
                    return null;
                }

                if (File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint)
                    || new FileInfo(path).Length is <= 0 or > 64 * 1024
                    || OperatingSystem.IsWindows() && !ServiceOwnedDirectoryProvisioner.IsTrustedControlFile(path))
                {
                    HasUnreadableState = true;
                    return null;
                }

                var state = JsonSerializer.Deserialize<AgentPackageLifecycleState>(File.ReadAllText(path), JsonOptions);
                if (state is null || string.IsNullOrWhiteSpace(state.OperationId) || string.IsNullOrWhiteSpace(state.PackageId) || string.IsNullOrWhiteSpace(state.PackageVersion))
                {
                    HasUnreadableState = true;
                    return null;
                }

                return state;
            }
            catch
            {
                // A malformed or unreadable checkpoint is itself recovery-required. The platform
                // exposes this through HasUnreadableState instead of beginning another mutation.
                HasUnreadableState = true;
                return null;
            }
        }
    }

    public bool HasUnreadableState { get; private set; }

    public void Write(AgentPackageLifecycleState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        options.EnsureStorageProvisioned();
        lock (gate)
        {
            var root = options.PackageRoot;
            var temporary = Path.Combine(root, ".lifecycle-state." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                var json = JsonSerializer.Serialize(state, JsonOptions);
                File.WriteAllText(temporary, json);
                if (File.GetAttributes(temporary).HasFlag(FileAttributes.ReparsePoint)) throw new IOException("The lifecycle checkpoint became a reparse point.");
                File.Move(temporary, options.LifecycleStatePath, overwrite: true);
                ServiceOwnedDirectoryProvisioner.EnsureControlFileAcl(options.LifecycleStatePath);
                HasUnreadableState = false;
            }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); } catch { }
            }
        }
    }

    public void Clear()
    {
        options.EnsureStorageProvisioned();
        lock (gate)
        {
            if (File.Exists(options.LifecycleStatePath)
                && !File.GetAttributes(options.LifecycleStatePath).HasFlag(FileAttributes.ReparsePoint)
                && (!OperatingSystem.IsWindows() || ServiceOwnedDirectoryProvisioner.IsTrustedControlFile(options.LifecycleStatePath)))
            {
                File.Delete(options.LifecycleStatePath);
            }

            HasUnreadableState = false;
        }
    }
}
