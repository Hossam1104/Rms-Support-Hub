using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;
using RmsSupportHub.Pos.Infrastructure.Configuration;

namespace RmsSupportHub.Pos.Infrastructure.Audit;

/// <summary>
/// Appends a bounded, redacted JSONL audit stream beneath the service-owned root. A failed audit
/// write is retained only in memory for the current process and never turns an already-dispatched
/// operation into a second mutation.
/// </summary>
public sealed class FileAgentAuditSink(
    AgentAuditOptions options,
    TimeProvider timeProvider) : IAgentAuditSink, IAgentAuditReader, IRmsPrivilegedAuditSink
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly object gate = new();
    private readonly List<AgentAuditEvent> fallback = [];

    public void Record(AgentAuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        options.Validate();
        var safe = Sanitize(auditEvent);
        lock (gate)
        {
            try
            {
                ServiceOwnedDirectoryProvisioner.EnsureProvisioned(options.RootPath);
                var path = Path.Combine(options.RootPath, "events.jsonl");
                var line = JsonSerializer.Serialize(safe, JsonOptions) + Environment.NewLine;
                RotateIfNeeded(path, Encoding.UTF8.GetByteCount(line));
                if (Encoding.UTF8.GetByteCount(line) <= options.MaximumBytes)
                {
                    File.AppendAllText(path, line, Encoding.UTF8);
                }
            }
            catch
            {
                // Audit must never cause a completed privileged operation to be replayed. Keep a
                // bounded process-local copy so a later Support Bundle can still report that the
                // durable sink was unavailable without emitting the failed exception.
            }

            fallback.Add(safe);
            while (fallback.Count > options.MaximumEntries) fallback.RemoveAt(0);
        }
    }

    public void Record(RmsPrivilegedAuditEvent auditEvent) => Record(new AgentAuditEvent(
        auditEvent.AtUtc,
        auditEvent.PrincipalSid,
        $"database.{auditEvent.Operation}.{auditEvent.Kind}",
        auditEvent.Database.ToString().ToLowerInvariant(),
        auditEvent.CorrelationId,
        auditEvent.Kind.ToString(),
        null,
        "unavailable",
        null));

    public Task<IReadOnlyList<AgentAuditEvent>> ReadRecentAsync(
        int maximumEntries,
        CancellationToken cancellationToken = default)
    {
        options.Validate();
        var boundedMaximum = Math.Clamp(maximumEntries, 1, options.MaximumEntries);
        lock (gate)
        {
            var records = new List<AgentAuditEvent>();
            try
            {
                var path = Path.Combine(options.RootPath, "events.jsonl");
                if (File.Exists(path) && !File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))
                {
                    foreach (var line in File.ReadLines(path).TakeLast(boundedMaximum))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        try
                        {
                            var item = JsonSerializer.Deserialize<AgentAuditEvent>(line, JsonOptions);
                            if (item is not null) records.Add(Sanitize(item));
                        }
                        catch (JsonException) { }
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                // Fall through to the bounded in-memory evidence below.
            }

            if (records.Count == 0) records.AddRange(fallback.TakeLast(boundedMaximum));
            return Task.FromResult<IReadOnlyList<AgentAuditEvent>>(records.AsEnumerable().Reverse().ToArray());
        }
    }

    private void RotateIfNeeded(string path, int incomingBytes)
    {
        if (!File.Exists(path)) return;
        var info = new FileInfo(path);
        if (info.Length + incomingBytes <= options.MaximumBytes) return;
        var archive = path + ".previous";
        if (File.Exists(archive)) File.Delete(archive);
        File.Move(path, archive);
    }

    private AgentAuditEvent Sanitize(AgentAuditEvent item) => new(
        item.AtUtc == default ? timeProvider.GetUtcNow() : item.AtUtc,
        SafePrincipal(item.Principal),
        SafeToken(item.Operation, "unknown.operation") ?? "unknown.operation",
        SafeOpaque(item.Target),
        SafeToken(item.CorrelationId, "unavailable") ?? "unavailable",
        SafeToken(item.Outcome, "unknown") ?? "unknown",
        SafeToken(item.FailureCode, null),
        SafeToken(item.ProductVersion, "unavailable") ?? "unavailable",
        SafeToken(item.BuildId, null))
    {
        PackageId = SafeToken(item.PackageId, null),
        PackageVersion = SafeToken(item.PackageVersion, null),
        TrustResult = SafeToken(item.TrustResult, null),
        RecoveryState = SafeToken(item.RecoveryState, null)
    };

    private static string SafePrincipal(string? value) =>
        SafeToken(value, "unavailable") is { } principal && principal.Length <= 256
            ? principal
            : "unavailable";

    private static string? SafeOpaque(string? value) =>
        string.IsNullOrWhiteSpace(value)
            || value.Length > 128
            || value.Any(character => char.IsControl(character) || char.IsWhiteSpace(character) || character is '/' or '\\' or ':' or '?' or '#')
            ? null
            : value;

    private static string? SafeToken(string? value, string? fallback)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 256 || value.Any(char.IsControl)) return fallback;
        return value.Trim();
    }
}
