namespace RmsSupportHub.Pos.Agent.MutationTokens;

public sealed record MutationTokenIssue(string Token, DateTimeOffset ExpiresAtUtc);

public sealed record MutationTokenValidationRequest(
    string Token,
    string PrincipalSid,
    string Origin,
    string Method,
    string OperationId);

public enum MutationTokenFailure
{
    None,
    Missing,
    Unknown,
    Expired,
    Mismatch,
    Capacity
}

public readonly record struct MutationTokenConsumeResult(bool Succeeded, MutationTokenFailure Failure)
{
    public static MutationTokenConsumeResult Success { get; } = new(true, MutationTokenFailure.None);
}

public interface IMutationTokenStore
{
    MutationTokenIssue Issue(string principalSid, string origin, string method, string operationId);

    MutationTokenConsumeResult TryConsume(MutationTokenValidationRequest request);
}
