using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Application.Packages;

/// <summary>
/// Determines whether a permanent Agent installation can be adopted or a known legacy service
/// can be migrated. It never changes SCM state and never treats a name-only match as ownership.
/// </summary>
public sealed class AgentMigrationPolicy
{
    public AgentMigrationDecision Evaluate(
        AgentServiceEvidence permanent,
        IReadOnlyList<AgentServiceEvidence> legacyServices,
        string installRoot,
        string packageId,
        string? packageVersion = null,
        string? binarySha256 = null)
    {
        var permanentAssessment = AgentOwnershipPolicy.AssessPermanent(
            permanent,
            installRoot,
            packageId,
            packageVersion,
            binarySha256);
        var legacyAssessments = legacyServices
            .Select(service => AgentLegacyServiceCatalog.TryGet(service.ServiceName, out var definition)
                ? AgentOwnershipPolicy.AssessLegacy(service, definition, installRoot, packageId, binarySha256)
                : new AgentOwnershipAssessment(
                    AgentResourceOwnershipState.Unowned,
                    "legacy_service_not_allow_listed",
                    "The inspected legacy service is not one of the known Support Hub-owned historical services.",
                    service with { State = AgentResourceOwnershipState.Unowned }))
            .ToArray();

        if (permanentAssessment.State == AgentResourceOwnershipState.Owned)
        {
            return new(AgentMigrationAction.NoOp, "permanent_agent_owned", "The permanent RMS Support Agent is already installed and independently owned; no legacy service may be adopted.", permanentAssessment, legacyAssessments);
        }

        if (legacyAssessments.Any(item => item.State == AgentResourceOwnershipState.Unowned || item.State == AgentResourceOwnershipState.Conflict))
        {
            return new(AgentMigrationAction.Conflict, "legacy_service_conflict", "A legacy service was found without complete independent ownership evidence. The service must remain untouched.", permanentAssessment, legacyAssessments);
        }

        if (legacyAssessments.Any(item => item.State == AgentResourceOwnershipState.Owned))
        {
            return new(AgentMigrationAction.MigrateOwnedLegacy, "owned_legacy_migration_available", "Only a known, independently owned legacy Support Hub service may be migrated to the permanent Agent identity.", permanentAssessment, legacyAssessments);
        }

        return new(AgentMigrationAction.Conflict, "agent_installation_unproven", "The permanent Agent is not proven owned and no independently owned legacy service was found.", permanentAssessment, legacyAssessments);
    }
}
