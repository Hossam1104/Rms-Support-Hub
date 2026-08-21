using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using RmsSupportHub.Api.Exceptions;
using RmsSupportHub.Core.Models;
using RmsSupportHub.Core.Modules;

namespace RmsSupportHub.Api.Security;

public interface IProductionMutationGate
{
    ProductionUnlockResult Unlock(string moduleKey, string sessionId, string password);

    void RequireUnlocked(
        IOrderModule module,
        ModuleEnvironment environment,
        string? token,
        string sessionId,
        string operation);
}

public sealed record ProductionUnlockResult(string Token, DateTimeOffset ExpiresAt);

/// <summary>
/// Server-side, in-memory Production mutation gate. Tokens are opaque,
/// session-bound, module/environment-scoped, short-lived and never persisted.
/// The owner secret is retained only as non-string bytes for comparison and
/// is never logged or returned.</summary>
public sealed class ProductionMutationGate : IProductionMutationGate
{
    public const string UnlockHeaderName = "X-SupportHub-Production-Unlock";

    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan AttemptWindow = TimeSpan.FromMinutes(1);
    private const int MaxFailedAttempts = 5;

    private readonly byte[]? _passwordBytes;
    private readonly ILogger<ProductionMutationGate> _logger;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly ConcurrentDictionary<string, UnlockSession> _tokens = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, AttemptWindowState> _attempts = new(StringComparer.Ordinal);

    public ProductionMutationGate(
        string? password,
        ILogger<ProductionMutationGate> logger,
        Func<DateTimeOffset>? utcNow = null)
    {
        _passwordBytes = string.IsNullOrEmpty(password)
            ? null
            : Encoding.UTF8.GetBytes(password);
        _logger = logger;
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public ProductionUnlockResult Unlock(string moduleKey, string sessionId, string password)
    {
        if (_passwordBytes is null)
        {
            _logger.LogWarning("Production unlock unavailable: owner-configured secret is missing.");
            throw new ProductionUnlockUnavailableException();
        }

        var now = _utcNow();
        var attemptKey = $"{sessionId}:{moduleKey}";
        if (!TryConsumeAttempt(attemptKey, now))
        {
            _logger.LogWarning("Production unlock throttled for module {ModuleKey}.", moduleKey);
            throw new ProductionUnlockFailedException();
        }

        if (!FixedTimeEquals(_passwordBytes, password ?? string.Empty))
        {
            _logger.LogWarning("Production unlock failed for module {ModuleKey}.", moduleKey);
            throw new ProductionUnlockFailedException();
        }

        _attempts.TryRemove(attemptKey, out _);
        var token = CreateOpaqueToken();
        var expiresAt = now.Add(TokenLifetime);
        _tokens[token] = new UnlockSession(moduleKey, "Production", sessionId, expiresAt);

        _logger.LogInformation("Production unlock succeeded for module {ModuleKey}.", moduleKey);
        return new ProductionUnlockResult(token, expiresAt);
    }

    public void RequireUnlocked(
        IOrderModule module,
        ModuleEnvironment environment,
        string? token,
        string sessionId,
        string operation)
    {
        if (!string.Equals(environment.Environment, "Production", StringComparison.OrdinalIgnoreCase))
            return;

        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Blocked Production mutation {Operation} for module {ModuleKey}: unlock required.", operation, module.Key);
            throw new ProductionMutationLockedException();
        }

        if (!_tokens.TryGetValue(token, out var session))
        {
            _logger.LogWarning("Blocked Production mutation {Operation} for module {ModuleKey}: invalid unlock.", operation, module.Key);
            throw new ProductionMutationLockedException();
        }

        var now = _utcNow();
        if (session.ExpiresAt <= now)
        {
            _tokens.TryRemove(token, out _);
            _logger.LogWarning("Blocked expired Production mutation {Operation} for module {ModuleKey}.", operation, module.Key);
            throw new ProductionUnlockExpiredException();
        }

        if (!string.Equals(session.ModuleKey, module.Key, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(session.EnvironmentKey, environment.Environment, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(session.SessionId, sessionId, StringComparison.Ordinal))
        {
            _logger.LogWarning("Blocked cross-scope Production mutation {Operation} for module {ModuleKey}.", operation, module.Key);
            throw new ProductionMutationLockedException();
        }

        _logger.LogInformation("Production mutation unlock accepted for module {ModuleKey}, operation {Operation}.", module.Key, operation);
    }

    private bool TryConsumeAttempt(string key, DateTimeOffset now)
    {
        while (true)
        {
            if (!_attempts.TryGetValue(key, out var current) || current.WindowStarted + AttemptWindow <= now)
            {
                if (_attempts.TryAdd(key, new AttemptWindowState(now, 1)))
                    return true;
                continue;
            }

            if (current.Count >= MaxFailedAttempts)
                return false;

            if (_attempts.TryUpdate(key, current with { Count = current.Count + 1 }, current))
                return true;
        }
    }

    private static bool FixedTimeEquals(byte[] expected, string supplied)
    {
        var suppliedBytes = Encoding.UTF8.GetBytes(supplied);
        var length = Math.Max(expected.Length, suppliedBytes.Length);
        var expectedPadded = new byte[length];
        var suppliedPadded = new byte[length];
        Buffer.BlockCopy(expected, 0, expectedPadded, 0, expected.Length);
        Buffer.BlockCopy(suppliedBytes, 0, suppliedPadded, 0, suppliedBytes.Length);
        return CryptographicOperations.FixedTimeEquals(expectedPadded, suppliedPadded)
            && expected.Length == suppliedBytes.Length;
    }

    private static string CreateOpaqueToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

    private sealed record UnlockSession(string ModuleKey, string EnvironmentKey, string SessionId, DateTimeOffset ExpiresAt);
    private sealed record AttemptWindowState(DateTimeOffset WindowStarted, int Count);
}
