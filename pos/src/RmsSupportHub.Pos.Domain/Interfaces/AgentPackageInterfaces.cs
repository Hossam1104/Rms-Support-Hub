using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Domain.Interfaces;

public interface IAgentPackagePolicy
{
    AgentPackageValidationResult ValidateManifest(AgentPackageManifest manifest);
}
