using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Domain.Tests;

public sealed class AgentDeploymentTests
{
    [Fact]
    public void PermanentServiceRequiresIndependentIdentityAndOwnedInstallRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "rms-agent", "install");
        var evidence = new AgentServiceEvidence(
            AgentProductIdentity.PermanentServiceName,
            AgentProductIdentity.ServiceDisplayName,
            AgentProductIdentity.ServiceDescription,
            Path.Combine(root, "RmsSupportAgent.exe"),
            AgentProductIdentity.ProductId,
            "1.0.0",
            AgentProductIdentity.ProductId,
            new string('a', 64),
            AgentResourceOwnershipState.Unknown);

        var assessment = AgentOwnershipPolicy.AssessPermanent(evidence, root, AgentProductIdentity.ProductId, "1.0.0", new string('a', 64));

        Assert.Equal(AgentResourceOwnershipState.Owned, assessment.State);
        Assert.Equal("owned", assessment.Code);
    }

    [Fact]
    public void NameOnlyOrPathMismatchIsAConflict()
    {
        var evidence = new AgentServiceEvidence(
            AgentProductIdentity.PermanentServiceName,
            AgentProductIdentity.ServiceDisplayName,
            AgentProductIdentity.ServiceDescription,
            @"C:\Unknown\RmsSupportAgent.exe",
            AgentProductIdentity.ProductId,
            "1.0.0",
            AgentProductIdentity.ProductId,
            new string('a', 64),
            AgentResourceOwnershipState.Unknown);

        var assessment = AgentOwnershipPolicy.AssessPermanent(evidence, @"C:\Program Files\DBS\RmsSupportAgent", AgentProductIdentity.ProductId);

        Assert.Equal(AgentResourceOwnershipState.Conflict, assessment.State);
        Assert.Equal("permanent_service_ownership_unproven", assessment.Code);
    }

    [Fact]
    public void RMSProductServicesAreNeverHistoricalSupportAgentServices()
    {
        Assert.True(AgentProductIdentity.IsRmsProductService("RMS.BranchService"));
        Assert.False(AgentProductIdentity.IsHistoricalTestingService("RMS.BranchService"));
        Assert.True(AgentProductIdentity.IsHistoricalTestingService("RmsSupportHub.Pos.Int13.TestService"));
    }
}
