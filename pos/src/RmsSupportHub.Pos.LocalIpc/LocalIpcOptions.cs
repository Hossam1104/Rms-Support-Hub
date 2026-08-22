using RmsSupportHub.Pos.Contracts.V1.LocalIpc;

namespace RmsSupportHub.Pos.LocalIpc;

public sealed class LocalIpcOptions
{
    public const string SectionName = "PosAgent:LocalIpc";

    public bool Enabled { get; init; }

    public string PipeName { get; init; } = LocalIpcProtocol.PipeName;

    public string OperatorGroupName { get; init; } = "RMS Support Operators";

    public int MaxRequestBytes { get; init; } = 64 * 1024;

    public int MaxResponseBytes { get; init; } = 256 * 1024;

    public TimeSpan ConnectionTimeout { get; init; } = TimeSpan.FromSeconds(10);

    public TimeSpan ReadTimeout { get; init; } = TimeSpan.FromSeconds(10);

    public int MaxConcurrentClients { get; init; } = 4;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PipeName)
            || PipeName.Length > 256
            || PipeName.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)
                || character is '\\' or '/'))
        {
            throw new ArgumentException("The local IPC pipe name must be a bounded local name.", nameof(PipeName));
        }

        if (string.IsNullOrWhiteSpace(OperatorGroupName)
            || OperatorGroupName.Length > 256
            || OperatorGroupName.Any(char.IsControl))
        {
            throw new ArgumentException("A bounded local operator group name is required.", nameof(OperatorGroupName));
        }

        if (MaxRequestBytes is < 1024 or > 1024 * 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxRequestBytes));
        }

        if (MaxResponseBytes is < 1024 or > 4 * 1024 * 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxResponseBytes));
        }

        if (ConnectionTimeout < TimeSpan.FromMilliseconds(100)
            || ConnectionTimeout > TimeSpan.FromMinutes(1))
        {
            throw new ArgumentOutOfRangeException(nameof(ConnectionTimeout));
        }

        if (ReadTimeout < TimeSpan.FromMilliseconds(100)
            || ReadTimeout > TimeSpan.FromMinutes(1))
        {
            throw new ArgumentOutOfRangeException(nameof(ReadTimeout));
        }

        if (MaxConcurrentClients is < 1 or > 32)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxConcurrentClients));
        }
    }
}
