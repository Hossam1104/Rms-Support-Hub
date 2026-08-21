using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using RmsSupportHub.Api.Exceptions;
using RmsSupportHub.Core.Models;
using RmsSupportHub.Core.Modules;

namespace RmsSupportHub.Api.Security;

public interface IProductionMutationGate
{
    ProductionUnlockResult Unlock(
        string moduleKey,
        string sessionId,
        string password,
        string? remoteIpAddress = null);

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
public sealed class ProductionMutationGate : IProductionMutationGate, IDisposable
{
    public const string UnlockHeaderName = "X-SupportHub-Production-Unlock";

    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan AttemptWindow = TimeSpan.FromMinutes(1);
    private const int MaxFailedAttemptsPerSession = 5;
    private const int MaxFailedAttemptsPerSource = 5;
    private const int MaxFailedAttemptsPerModule = 20;
    private const int MaxTrackedEntries = 4096;

    private readonly byte[]? _passwordBytes;
    private readonly ILogger<ProductionMutationGate> _logger;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly TimeSpan _tokenLifetime;
    private readonly TimeSpan _attemptWindow;
    private readonly MemoryCache _tokens;
    private readonly MemoryCache _attempts;
    private readonly object _attemptsLock = new();

    public ProductionMutationGate(
        string? password,
        ILogger<ProductionMutationGate> logger,
        Func<DateTimeOffset>? utcNow = null)
        : this(password, logger, utcNow, TokenLifetime, AttemptWindow)
    {
    }

    internal ProductionMutationGate(
        string? password,
        ILogger<ProductionMutationGate> logger,
        Func<DateTimeOffset>? utcNow,
        TimeSpan tokenLifetime,
        TimeSpan attemptWindow)
    {
        _passwordBytes = string.IsNullOrEmpty(password)
            ? null
            : Encoding.UTF8.GetBytes(password);
        _logger = logger;
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
        _tokenLifetime = tokenLifetime;
        _attemptWindow = attemptWindow;
        _tokens = CreateCache();
        _attempts = CreateCache();
    }

    public ProductionUnlockResult Unlock(
        string moduleKey,
        string sessionId,
        string password,
        string? remoteIpAddress = null)
    {
        if (_passwordBytes is null)
        {
            _logger.LogWarning("Production unlock unavailable: owner-configured secret is missing.");
            throw new ProductionUnlockUnavailableException();
        }

        var now = _utcNow();
        var sessionPartition = BuildPartition("session", moduleKey, sessionId);
        var sourcePartition = BuildPartition(
            "source",
            moduleKey,
            string.IsNullOrWhiteSpace(remoteIpAddress) ? "unknown" : remoteIpAddress);
        var modulePartition = BuildPartition("module", moduleKey, "all");

        if (!TryConsumeAttempt(sessionPartition, now, MaxFailedAttemptsPerSession)
            || !TryConsumeAttempt(sourcePartition, now, MaxFailedAttemptsPerSource)
            || !TryConsumeAttempt(modulePartition, now, MaxFailedAttemptsPerModule))
        {
            _logger.LogWarning("Production unlock throttled for module {ModuleKey}.", moduleKey);
            throw new ProductionUnlockFailedException();
        }

        if (!FixedTimeEquals(_passwordBytes, password ?? string.Empty))
        {
            _logger.LogWarning("Production unlock failed for module {ModuleKey}.", moduleKey);
            throw new ProductionUnlockFailedException();
        }

        RemoveAttemptPartitions(sessionPartition, sourcePartition, modulePartition);
        var token = CreateOpaqueToken();
        var expiresAt = now.Add(_tokenLifetime);
        _tokens.Set(
            token,
            new UnlockSession(moduleKey, "Production", sessionId, expiresAt),
            EntryOptions(_tokenLifetime));

        // A full bounded cache must fail closed rather than return a token
        // which the server could not retain for later mutation authorization.
        if (!_tokens.TryGetValue(token, out _))
            throw new ProductionUnlockFailedException();

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

        if (!_tokens.TryGetValue(token, out var sessionValue)
            || sessionValue is not UnlockSession session)
        {
            _logger.LogWarning("Blocked Production mutation {Operation} for module {ModuleKey}: invalid unlock.", operation, module.Key);
            throw new ProductionMutationLockedException();
        }

        var now = _utcNow();
        if (session.ExpiresAt <= now)
        {
            _tokens.Remove(token);
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

    private bool TryConsumeAttempt(string key, DateTimeOffset now, int maximum)
    {
        // The lock makes logical expiry and replacement one operation.
        // MemoryCache supplies automatic expiry when entries are never read;
        // the injected clock comparison also handles deterministic tests
        // advancing beyond the window without a TryAdd spin loop.
        lock (_attemptsLock)
        {
            if (_attempts.TryGetValue(key, out var currentValue)
                && currentValue is AttemptWindowState current
                && current.WindowStarted + _attemptWindow > now)
            {
                if (current.Count >= maximum)
                    return false;

                _attempts.Set(
                    key,
                    current with { Count = current.Count + 1 },
                    EntryOptions(_attemptWindow));
                return _attempts.TryGetValue(key, out _);
            }

            _attempts.Set(key, new AttemptWindowState(now, 1), EntryOptions(_attemptWindow));
            return _attempts.TryGetValue(key, out _);
        }
    }

    private void RemoveAttemptPartitions(params string[] partitions)
    {
        lock (_attemptsLock)
        {
            foreach (var partition in partitions)
                _attempts.Remove(partition);
        }
    }

    private static MemoryCache CreateCache() => new(new MemoryCacheOptions
    {
        SizeLimit = MaxTrackedEntries,
        ExpirationScanFrequency = TimeSpan.FromSeconds(1)
    });

    private static string BuildPartition(string kind, string moduleKey, string identity) =>
        $"{kind}:{moduleKey}:{identity}";

    private static MemoryCacheEntryOptions EntryOptions(TimeSpan lifetime) =>
        new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(lifetime)
            .SetSize(1);

    internal int TrackedTokenCount => _tokens.Count;
    internal int TrackedAttemptCount => _attempts.Count;

    public void Dispose()
    {
        _tokens.Dispose();
        _attempts.Dispose();
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
