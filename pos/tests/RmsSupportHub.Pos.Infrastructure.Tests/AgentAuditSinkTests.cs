using RmsSupportHub.Pos.Domain.Models;
using RmsSupportHub.Pos.Infrastructure.Audit;

namespace RmsSupportHub.Pos.Infrastructure.Tests;

public sealed class AgentAuditSinkTests
{
    [Fact]
    public async Task AuditSinkPersistsBoundedSafeFieldsAndDoesNotEchoSecrets()
    {
        var root = Path.Combine(Path.GetTempPath(), "rms-audit", Guid.NewGuid().ToString("N"));
        try
        {
            var sink = new FileAgentAuditSink(new AgentAuditOptions { RootPath = root }, TimeProvider.System);
            sink.Record(new AgentAuditEvent(
                DateTimeOffset.UtcNow,
                "S-1-5-21-operator",
                "service-control",
                "svc-branch",
                "correlation-1",
                "completed",
                null,
                "1.0.0",
                null));

            var events = await sink.ReadRecentAsync(10);

            Assert.Single(events);
            Assert.Equal("svc-branch", events[0].Target);
            Assert.DoesNotContain("password", await File.ReadAllTextAsync(Path.Combine(root, "events.jsonl")), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { }
        }
    }
}
