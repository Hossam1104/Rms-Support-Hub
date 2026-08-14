namespace RmsSupportHub.Pos.Agent.MutationTokens;

public sealed class MutationTokenOptions
{
    public int MaxTokens { get; init; } = 256;

    public TimeSpan Lifetime { get; init; } = TimeSpan.FromSeconds(60);

    public void Validate()
    {
        if (MaxTokens < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxTokens));
        }

        if (Lifetime <= TimeSpan.Zero || Lifetime > TimeSpan.FromHours(1))
        {
            throw new ArgumentOutOfRangeException(nameof(Lifetime));
        }
    }
}
