using RmsSupportHub.Pos.Application.Diagnostics;
using RmsSupportHub.Pos.Application.Invocation;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Application.Tests;

public sealed class AgentInvocationAndDiscoveryTests
{
    [Fact]
    public async Task LocalOperatorCanInvokeTheSharedInstallationDiscoveryHandler()
    {
        var audit = new RecordingAuditSink();
        var expected = CreateSnapshot();
        var handler = new RmsInstallationDiscoveryQueryHandler(
            new FakeDiscovery(expected),
            audit,
            TimeProvider.System);

        var result = await handler.HandleAsync(new InvocationContext(
            InvocationSource.LocalWpf,
            "S-1-5-21-operator",
            InvocationAuthorizationLevel.LocalOperator,
            "corr-local"));

        Assert.True(result.Succeeded);
        Assert.Equal(expected, result.Value);
        var record = Assert.Single(audit.Events);
        Assert.Equal("LocalWpf", record.Source);
        Assert.Equal("rms.installation.discover", record.Operation);
        Assert.Equal("corr-local", record.CorrelationId);
        Assert.Equal("completed", record.Outcome);
    }

    [Fact]
    public async Task LegacyLoopbackHttpAdministratorUsesTheSameApplicationHandler()
    {
        var expected = CreateSnapshot();
        var handler = new RmsInstallationDiscoveryQueryHandler(
            new FakeDiscovery(expected),
            new RecordingAuditSink(),
            TimeProvider.System);

        var result = await handler.HandleAsync(new InvocationContext(
            InvocationSource.LegacyLoopbackHttp,
            "S-1-5-21-admin",
            InvocationAuthorizationLevel.LocalAdministrator,
            "corr-http"));

        Assert.True(result.Succeeded);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task MissingContextFailsClosedAndIsAudited()
    {
        var audit = new RecordingAuditSink();
        var handler = new RmsInstallationDiscoveryQueryHandler(
            new FakeDiscovery(CreateSnapshot()),
            audit,
            TimeProvider.System);

        var result = await handler.HandleAsync(null);

        Assert.False(result.Succeeded);
        Assert.Equal("invocation_context_missing", result.Error?.Code);
        Assert.Equal("denied", Assert.Single(audit.Events).Outcome);
    }

    [Fact]
    public void OperatorIsDeniedAdministratorOnlyClassification()
    {
        var decision = AgentOperationAuthorization.Authorize(
            new InvocationContext(
                InvocationSource.LocalWpf,
                "S-1-5-21-operator",
                InvocationAuthorizationLevel.LocalOperator,
                "corr"),
            AgentOperationRisk.AdministratorOnlyMutation);

        Assert.False(decision.Allowed);
        Assert.Equal("administrator_authorization_required", decision.Code);
    }

    [Fact]
    public void UnsafeCorrelationContextFailsClosed()
    {
        var decision = AgentOperationAuthorization.Authorize(
            new InvocationContext(
                InvocationSource.LocalWpf,
                "S-1-5-21-operator",
                InvocationAuthorizationLevel.LocalOperator,
                "bad\r\ncorrelation"),
            AgentOperationRisk.ReadOnlyDiagnostic);

        Assert.False(decision.Allowed);
        Assert.Equal("invocation_context_invalid", decision.Code);
    }

    private static RmsInstallationSnapshot CreateSnapshot() => new(
        InstallationDetected: true,
        BranchInstalled: true,
        CashierInstalled: false,
        BranchCode: "B001",
        PosNumber: "POS01",
        MainServerBranchId: "branch-1",
        MainServerPosId: "pos-1",
        InstallationGuid: "installation-1",
        MainServerUrl: "https://main.example.test:443",
        BranchServerAddress: "branch.example.test:443",
        ClientName: "Testing",
        ProductRelease: "10.0.0",
        Versions: new("10.0.0", null, null),
        Consistency: new(
            RmsConsistencyState.Consistent,
            RmsConsistencyState.Consistent,
            RmsConsistencyState.Consistent,
            RmsConsistencyState.Consistent,
            RmsConsistencyState.Consistent,
            []),
        BranchDatabase: new(
            true,
            RmsConnectionStringState.Valid,
            "sql.example.test",
            "BranchServer",
            true),
        CashierDatabase: new(
            false,
            RmsConnectionStringState.Unavailable,
            null,
            null,
            null),
        MainServerEndpoint: new(
            RmsEndpointConfigurationState.Configured,
            "main.example.test",
            443,
            "main.example.test:443"),
        BranchServerEndpoint: new(
            RmsEndpointConfigurationState.Configured,
            "branch.example.test",
            443,
            "branch.example.test:443"),
        Services: new(true, true, true, false),
        ComponentDrift: []);

    private sealed class FakeDiscovery(RmsInstallationSnapshot snapshot) : IRmsInstallationDiscovery
    {
        public Task<RmsInstallationSnapshot> DiscoverAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(snapshot);
    }

    private sealed class RecordingAuditSink : IAgentAuditSink
    {
        public List<AgentAuditEvent> Events { get; } = [];

        public void Record(AgentAuditEvent auditEvent) => Events.Add(auditEvent);
    }
}
