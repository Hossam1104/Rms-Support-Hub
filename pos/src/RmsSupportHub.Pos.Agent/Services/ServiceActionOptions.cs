namespace RmsSupportHub.Pos.Agent.Services;

public sealed class ServiceActionOptions
{
    public const int MaxIdempotencyKeyLength = 128;

    public int MaxIdempotencyEntries { get; init; } = 256;

    public TimeSpan IdempotencyRetention { get; init; } = TimeSpan.FromMinutes(15);

    public TimeSpan DispatchTimeout { get; init; } = TimeSpan.FromSeconds(35);

    public void Validate()
    {
        if (MaxIdempotencyEntries < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxIdempotencyEntries));
        }

        if (IdempotencyRetention <= TimeSpan.Zero || IdempotencyRetention > TimeSpan.FromHours(24))
        {
            throw new ArgumentOutOfRangeException(nameof(IdempotencyRetention));
        }

        if (DispatchTimeout <= TimeSpan.Zero || DispatchTimeout > TimeSpan.FromMinutes(2))
        {
            throw new ArgumentOutOfRangeException(nameof(DispatchTimeout));
        }
    }
}
