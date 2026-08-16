using RmsSupportHub.Pos.Application.Packages;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Application.Tests;

public sealed class AgentMigrationAndCertificateTests
{
    [Fact]
    public void UnownedSameNameLegacyServiceBlocksMigration()
    {
        var root = Path.Combine(Path.GetTempPath(), "rms-agent", "install");
        var permanent = new AgentServiceEvidence(
            AgentProductIdentity.PermanentServiceName,
            AgentProductIdentity.ServiceDisplayName,
            AgentProductIdentity.ServiceDescription,
            null,
            null,
            null,
            null,
            null,
            AgentResourceOwnershipState.Missing);
        var legacy = new AgentServiceEvidence(
            "RmsSupportHub.Pos.Agent",
            "RMS+ POS Agent (Testing)",
            null,
            Path.Combine(root, "legacy.exe"),
            AgentProductIdentity.ProductId,
            "1.0.0",
            "not-owned",
            null,
            AgentResourceOwnershipState.Unknown);

        var decision = new AgentMigrationPolicy().Evaluate(permanent, [legacy], root, AgentProductIdentity.ProductId);

        Assert.Equal(AgentMigrationAction.Conflict, decision.Action);
        Assert.Equal("legacy_service_conflict", decision.Code);
    }

    [Fact]
    public void CorrectPermanentServiceIsNoOp()
    {
        var root = Path.Combine(Path.GetTempPath(), "rms-agent", "install");
        var permanent = new AgentServiceEvidence(
            AgentProductIdentity.PermanentServiceName,
            AgentProductIdentity.ServiceDisplayName,
            AgentProductIdentity.ServiceDescription,
            Path.Combine(root, "agent.exe"),
            AgentProductIdentity.ProductId,
            "1.0.0",
            AgentProductIdentity.ProductId,
            new string('a', 64),
            AgentResourceOwnershipState.Unknown);

        var decision = new AgentMigrationPolicy().Evaluate(permanent, [], root, AgentProductIdentity.ProductId, "1.0.0", new string('a', 64));

        Assert.Equal(AgentMigrationAction.NoOp, decision.Action);
    }

    [Fact]
    public void CertificatePolicyRequiresCngNonExportableServerAuthAndOwnership()
    {
        var now = new DateTimeOffset(2026, 8, 16, 12, 0, 0, TimeSpan.Zero);
        var valid = new AgentCertificateEvidence(
            "AABB",
            "rms-pos-agent.localhost",
            true,
            AgentCertificatePolicy.ExpectedProvider,
            false,
            true,
            AgentCertificatePolicy.ExpectedOwnershipMarker,
            now.AddDays(-1),
            now.AddDays(60),
            "LocalMachine",
            TrustedPrivateKeyEvidence());

        var assessment = new AgentCertificatePolicy().Assess(valid, now);

        Assert.True(assessment.Valid);
        Assert.False(assessment.RenewalRequired);
    }

    [Fact]
    public void CertificateRemovalRequiresOwnedThumbprintAndNeverAllowsEnterpriseRemoval()
    {
        var evidence = new AgentCertificateEvidence(
            "AABB",
            "rms-pos-agent.localhost",
            true,
            AgentCertificatePolicy.ExpectedProvider,
            false,
            true,
            AgentCertificatePolicy.EnterpriseOwnershipMarker,
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(60),
            "LocalMachine");

        Assert.False(new AgentCertificatePolicy().CanRemove(evidence, "AABB"));
    }

    [Fact]
    public void CertificatePolicyRejectsAdminOnlyPrivateKeyProof()
    {
        var now = DateTimeOffset.UtcNow;
        var evidence = new AgentCertificateEvidence(
            "AABB",
            "rms-pos-agent.localhost",
            true,
            AgentCertificatePolicy.ExpectedProvider,
            false,
            true,
            AgentCertificatePolicy.ExpectedOwnershipMarker,
            now.AddDays(-1),
            now.AddDays(60),
            "LocalMachine",
            TrustedPrivateKeyEvidence() with { LocalSystemReadAccess = false });

        var assessment = new AgentCertificatePolicy().Assess(evidence, now);

        Assert.False(assessment.Valid);
        Assert.Equal("local_system_private_key_access_unproven", assessment.Code);
    }

    [Fact]
    public void CertificatePolicyRejectsBroadPrivateKeyAllowRuleEvidence()
    {
        var now = DateTimeOffset.UtcNow;
        var evidence = new AgentCertificateEvidence(
            "AABB",
            "rms-pos-agent.localhost",
            true,
            AgentCertificatePolicy.ExpectedProvider,
            false,
            true,
            AgentCertificatePolicy.ExpectedOwnershipMarker,
            now.AddDays(-1),
            now.AddDays(60),
            "LocalMachine",
            TrustedPrivateKeyEvidence() with { HasUnsafeAllowRules = true });

        var assessment = new AgentCertificatePolicy().Assess(evidence, now);

        Assert.False(assessment.Valid);
        Assert.Equal("local_system_private_key_access_unproven", assessment.Code);
    }

    private static AgentPrivateKeySecurityEvidence TrustedPrivateKeyEvidence() =>
        new(
            IsMachineKey: true,
            IsCngKey: true,
            ProviderName: AgentCertificatePolicy.ExpectedProvider,
            IsExportable: false,
            KeyFileExists: true,
            KeyFileIsReparsePoint: false,
            OwnerTrusted: true,
            AccessRulesProtected: true,
            LocalSystemReadAccess: true,
            HasUnsafeAllowRules: false);
}
