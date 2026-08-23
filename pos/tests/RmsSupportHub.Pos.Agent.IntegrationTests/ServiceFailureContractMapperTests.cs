using RmsSupportHub.Pos.Agent.Diagnostics;
using RmsSupportHub.Pos.Application.Diagnostics;

namespace RmsSupportHub.Pos.Agent.IntegrationTests;

public sealed class ServiceFailureContractMapperTests
{
    [Fact]
    public void MapsTransportNeutralApplicationEvidenceToTheExistingV1Shape()
    {
        var checkedAt = new DateTimeOffset(2026, 8, 23, 10, 0, 0, TimeSpan.Zero);
        var analysis = new ServiceFailureAnalysis(
            "svc-0123456789abcdef",
            "RMS Branch Service",
            FailureCategory.Database,
            FailureSeverity.ActionRequired,
            FailureConfidence.High,
            "The service database is unavailable.",
            checkedAt,
            [new FailureEvidence(
                "Application Event Log",
                checkedAt,
                "Database failure summary.",
                "SqlException",
                ["at RMS.Branch.Worker.Run()"],
                "db-001")],
            ["database_unavailable"],
            [new FailureRecommendation("check-database", "Check database", "Verify the server-owned database health.")]);

        var mapped = ServiceFailureContractMapper.Map(analysis);

        Assert.Equal("svc-0123456789abcdef", mapped.ServiceId);
        Assert.Equal(RmsSupportHub.Pos.Contracts.V1.Diagnostics.FailureCategory.Database, mapped.Category);
        Assert.Equal(RmsSupportHub.Pos.Contracts.V1.Diagnostics.FailureSeverity.ActionRequired, mapped.Severity);
        Assert.Equal(RmsSupportHub.Pos.Contracts.V1.Diagnostics.FailureConfidence.High, mapped.Confidence);
        Assert.Equal(checkedAt, mapped.CheckedAtUtc);
        Assert.Equal("Application Event Log", Assert.Single(mapped.Evidence).Source);
        Assert.Equal("SqlException", Assert.Single(mapped.Evidence).ExceptionType);
        Assert.Equal("check-database", Assert.Single(mapped.Recommendations).Code);

        var snapshot = ServiceFailureContractMapper.Map(new LogEvidenceSnapshot(
            checkedAt,
            LogEvidenceOverallState.Degraded,
            [analysis]));

        Assert.Equal(
            RmsSupportHub.Pos.Contracts.V1.Diagnostics.LogEvidenceOverallState.Degraded,
            snapshot.OverallState);
        Assert.Equal("svc-0123456789abcdef", Assert.Single(snapshot.Services).ServiceId);
    }
}
