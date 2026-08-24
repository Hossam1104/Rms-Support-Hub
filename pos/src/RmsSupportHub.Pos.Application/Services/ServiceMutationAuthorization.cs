using System.Security.Cryptography;
using System.Text;
using RmsSupportHub.Pos.Domain.Enums;

namespace RmsSupportHub.Pos.Application.Services;

public sealed record ServiceMutationAuthorizationIssue(
    bool Succeeded,
    string Token,
    DateTimeOffset ExpiresAtUtc,
    string Code,
    string Detail)
{
    public static ServiceMutationAuthorizationIssue Failure(string code, string detail) =>
        new(false, string.Empty, default, code, detail);
}

public enum ServiceMutationAuthorizationFailure
{
    None,
    Missing,
    Unknown,
    Expired,
    Mismatch,
    Capacity
}

public readonly record struct ServiceMutationAuthorizationConsumeResult(
    bool Succeeded,
    ServiceMutationAuthorizationFailure Failure)
{
    public static ServiceMutationAuthorizationConsumeResult Success { get; } =
        new(true, ServiceMutationAuthorizationFailure.None);
}

/// <summary>
/// Process-local, bounded, atomic one-use authorization store for transport-neutral service
/// mutations. Only a SHA-256 fingerprint of the opaque token is retained; Agent restart
/// invalidates outstanding grants.
/// </summary>
public sealed class ServiceMutationAuthorizationStore
{
    private readonly object gate = new();
    private readonly TimeProvider clock;
    private readonly ServiceControlOptions options;
    private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);

    public ServiceMutationAuthorizationStore(ServiceControlOptions options, TimeProvider clock)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.options.Validate();
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public ServiceMutationAuthorizationIssue Issue(
        string principal,
        string serviceId,
        ServiceControlAction action,
        string confirmation,
        string correlationId)
    {
        var now = clock.GetUtcNow();
        var expiresAtUtc = now.Add(options.MutationAuthorizationLifetime);
        lock (gate)
        {
            PruneLocked(now);
            if (entries.Count >= options.MaxMutationAuthorizations)
            {
                return ServiceMutationAuthorizationIssue.Failure(
                    "mutation_authorization_capacity",
                    "The Agent cannot issue another service authorization at this time.");
            }

            string token;
            string fingerprint;
            do
            {
                token = CreateOpaqueToken();
                fingerprint = Fingerprint(token);
            }
            while (entries.ContainsKey(fingerprint));

            entries.Add(
                fingerprint,
                new(principal, serviceId, action, confirmation, correlationId, expiresAtUtc));
            return new(true, token, expiresAtUtc, "issued", "The service mutation authorization was issued.");
        }
    }

    public ServiceMutationAuthorizationConsumeResult TryConsume(
        string? token,
        string principal,
        string serviceId,
        ServiceControlAction action,
        string confirmation,
        string correlationId)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new(false, ServiceMutationAuthorizationFailure.Missing);
        }

        var fingerprint = Fingerprint(token);
        lock (gate)
        {
            if (!entries.Remove(fingerprint, out var entry))
            {
                return new(false, ServiceMutationAuthorizationFailure.Unknown);
            }

            if (clock.GetUtcNow() >= entry.ExpiresAtUtc)
            {
                return new(false, ServiceMutationAuthorizationFailure.Expired);
            }

            if (!string.Equals(entry.Principal, principal, StringComparison.Ordinal)
                || !string.Equals(entry.ServiceId, serviceId, StringComparison.Ordinal)
                || entry.Action != action
                || !string.Equals(entry.Confirmation, confirmation, StringComparison.Ordinal)
                || !string.Equals(entry.CorrelationId, correlationId, StringComparison.Ordinal))
            {
                // A mismatch consumes the grant too, preventing an observed opaque value from
                // being probed and then replayed against its correct target.
                return new(false, ServiceMutationAuthorizationFailure.Mismatch);
            }

            return ServiceMutationAuthorizationConsumeResult.Success;
        }
    }

    public int RetainedEntryCount
    {
        get
        {
            lock (gate)
            {
                PruneLocked(clock.GetUtcNow());
                return entries.Count;
            }
        }
    }

    public static string Fingerprint(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private void PruneLocked(DateTimeOffset now)
    {
        foreach (var pair in entries.ToArray())
        {
            if (now >= pair.Value.ExpiresAtUtc)
            {
                entries.Remove(pair.Key);
            }
        }
    }

    private static string CreateOpaqueToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private sealed record Entry(
        string Principal,
        string ServiceId,
        ServiceControlAction Action,
        string Confirmation,
        string CorrelationId,
        DateTimeOffset ExpiresAtUtc);
}
