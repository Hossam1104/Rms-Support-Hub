using System.Security.Cryptography;
using RmsSupportHub.Pos.Agent.Security;

namespace RmsSupportHub.Pos.Agent.MutationTokens;

/// <summary>
/// Process-local, bounded, atomic one-use token store. It is intentionally not persisted, so Agent
/// restart invalidates all outstanding tokens.
/// </summary>
public sealed class InMemoryMutationTokenStore : IMutationTokenStore
{
    private readonly object _gate = new();
    private readonly TimeProvider _timeProvider;
    private readonly MutationTokenOptions _options;
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    public InMemoryMutationTokenStore()
        : this(TimeProvider.System, new MutationTokenOptions())
    {
    }

    public InMemoryMutationTokenStore(TimeProvider timeProvider, MutationTokenOptions options)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();
    }

    public MutationTokenIssue Issue(
        string principalSid,
        string origin,
        string method,
        string operationId,
        string? httpPath = null)
    {
        if (!AgentPrincipal.TryCanonicalizeSid(principalSid, out var canonicalSid))
        {
            throw new ArgumentException("A canonical Windows SID is required.", nameof(principalSid));
        }

        if (!AgentSecurityOptions.TryNormalizeOrigin(origin, out var canonicalOrigin))
        {
            throw new ArgumentException("An exact HTTPS origin is required.", nameof(origin));
        }

        var canonicalMethod = NormalizeMethod(method);
        var canonicalOperationId = NormalizeOperationId(operationId);
        var canonicalPath = NormalizePath(httpPath);
        var now = _timeProvider.GetUtcNow();
        var expiresAtUtc = now.Add(_options.Lifetime);

        lock (_gate)
        {
            PruneLocked(now);
            if (_entries.Count >= _options.MaxTokens)
            {
                throw new MutationTokenCapacityException();
            }

            string token;
            do
            {
                token = CreateOpaqueToken();
            }
            while (_entries.ContainsKey(token));

            _entries.Add(token, new Entry(canonicalSid, canonicalOrigin, canonicalMethod, canonicalOperationId, canonicalPath, expiresAtUtc));
            return new MutationTokenIssue(token, expiresAtUtc);
        }
    }

    public MutationTokenConsumeResult TryConsume(MutationTokenValidationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return new(false, MutationTokenFailure.Missing);
        }

        if (!AgentPrincipal.TryCanonicalizeSid(request.PrincipalSid, out var canonicalSid)
            || !AgentSecurityOptions.TryNormalizeOrigin(request.Origin, out var canonicalOrigin))
        {
            return new(false, MutationTokenFailure.Mismatch);
        }

        string canonicalMethod;
        string canonicalOperationId;
        string? canonicalPath;
        try
        {
            canonicalMethod = NormalizeMethod(request.Method);
            canonicalOperationId = NormalizeOperationId(request.OperationId);
            canonicalPath = NormalizePath(request.HttpPath);
        }
        catch (ArgumentException)
        {
            return new(false, MutationTokenFailure.Mismatch);
        }

        lock (_gate)
        {
            if (!_entries.Remove(request.Token, out var entry))
            {
                return new(false, MutationTokenFailure.Unknown);
            }

            if (_timeProvider.GetUtcNow() >= entry.ExpiresAtUtc)
            {
                return new(false, MutationTokenFailure.Expired);
            }

            if (!string.Equals(entry.PrincipalSid, canonicalSid, StringComparison.Ordinal)
                || !string.Equals(entry.Origin, canonicalOrigin, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(entry.Method, canonicalMethod, StringComparison.Ordinal)
                || !string.Equals(entry.OperationId, canonicalOperationId, StringComparison.Ordinal)
                || !string.Equals(entry.HttpPath, canonicalPath, StringComparison.Ordinal))
            {
                // A mismatch consumes the token too. This prevents an attacker who observes an
                // opaque value from probing its binding and then replaying it correctly.
                return new(false, MutationTokenFailure.Mismatch);
            }

            return MutationTokenConsumeResult.Success;
        }
    }

    private void PruneLocked(DateTimeOffset now)
    {
        foreach (var pair in _entries.ToArray())
        {
            if (now >= pair.Value.ExpiresAtUtc)
            {
                _entries.Remove(pair.Key);
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

    private static string NormalizeMethod(string method)
    {
        if (string.IsNullOrWhiteSpace(method) || method.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException("A non-empty HTTP method is required.", nameof(method));
        }

        return method.ToUpperInvariant();
    }

    private static string NormalizeOperationId(string operationId)
    {
        if (string.IsNullOrWhiteSpace(operationId)
            || operationId.Length > 128
            || operationId.Any(character => char.IsControl(character) || char.IsWhiteSpace(character)))
        {
            throw new ArgumentException("A server-defined operation identifier is required.", nameof(operationId));
        }

        return operationId;
    }

    private static string? NormalizePath(string? httpPath)
    {
        if (string.IsNullOrEmpty(httpPath))
        {
            return null;
        }

        if (httpPath.Length > 2048
            || !httpPath.StartsWith("/", StringComparison.Ordinal)
            || httpPath.Contains("?", StringComparison.Ordinal)
            || httpPath.Contains("#", StringComparison.Ordinal)
            || httpPath.Any(character => char.IsControl(character) || char.IsWhiteSpace(character)))
        {
            throw new ArgumentException("A canonical HTTP path is required.", nameof(httpPath));
        }

        return httpPath;
    }

    private sealed record Entry(
        string PrincipalSid,
        string Origin,
        string Method,
        string OperationId,
        string? HttpPath,
        DateTimeOffset ExpiresAtUtc);
}

public sealed class MutationTokenCapacityException()
    : InvalidOperationException("The mutation-token retention limit has been reached.");
