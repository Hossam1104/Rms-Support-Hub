using RmsSupportHub.Pos.Contracts.V1.MainServer;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Agent.MainServer;

public sealed class MainServerService(
    IMainServerProfileCatalog profiles,
    IRmsInstallationDiscovery discovery,
    IMainServerReadOnlyClient readClient,
    TimeProvider clock)
{
    public async Task<MainServerProfilesDto> GetProfilesAsync(CancellationToken cancellationToken = default)
    {
        var installation = await discovery.DiscoverAsync(cancellationToken).ConfigureAwait(false);
        profiles.TryGetActive(installation, out var active, out var binding);
        return new(
            profiles.GetProfiles().Select(profile => ToDto(profile, profile.ProfileId == active.ProfileId ? binding.State :
                profile.Enabled ? MainServerBindingState.Unknown : MainServerBindingState.Unavailable)).ToArray(),
            active.ProfileId,
            Map(binding.State),
            binding.Detail);
    }

    public async Task<MainServerStateEvidenceDto> GetStateAsync(
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        var checkedAt = clock.GetUtcNow();
        correlationId ??= "unavailable";
        var installation = await discovery.DiscoverAsync(cancellationToken).ConfigureAwait(false);
        if (!profiles.TryGetActive(installation, out var profile, out var binding))
        {
            return new(
                profile.ProfileId,
                Map(profile.Environment),
                Map(binding.State),
                MainServerReadOutcomeDto.NotAttempted,
                binding.BranchCode,
                binding.PosNumber,
                binding.ClientName,
                null,
                null,
                binding.Detail,
                checkedAt,
                correlationId);
        }

        var result = await readClient.ReadStateAsync(profile, installation, cancellationToken).ConfigureAwait(false);
        return new(
            profile.ProfileId,
            Map(profile.Environment),
            Map(binding.State),
            Map(result.Outcome),
            binding.BranchCode,
            binding.PosNumber,
            binding.ClientName,
            result.BranchState,
            result.PosState,
            result.Detail,
            checkedAt,
            correlationId);
    }

    private static MainServerProfileDto ToDto(MainServerProfileDefinition profile, MainServerBindingState state) =>
        new(
            profile.ProfileId,
            Map(profile.Environment),
            profile.Enabled,
            Map(state),
            profile.ClientName,
            profile.AllowedReadOperations.Select(Map).ToArray(),
            profile.Enabled ? "Server-owned profile; the browser may request only fixed read projections." : "Future profile disabled until its separate production gates are complete.");

    private static MainServerEnvironmentDto Map(MainServerEnvironment value) => value switch
    {
        MainServerEnvironment.Testing => MainServerEnvironmentDto.Testing,
        _ => MainServerEnvironmentDto.Production
    };

    private static MainServerBindingStateDto Map(MainServerBindingState value) => value switch
    {
        MainServerBindingState.Bound => MainServerBindingStateDto.Bound,
        MainServerBindingState.Unavailable => MainServerBindingStateDto.Unavailable,
        MainServerBindingState.Mismatch => MainServerBindingStateDto.Mismatch,
        MainServerBindingState.Ambiguous => MainServerBindingStateDto.Ambiguous,
        _ => MainServerBindingStateDto.Unknown
    };

    private static MainServerReadOutcomeDto Map(MainServerReadOutcome value) => value switch
    {
        MainServerReadOutcome.Succeeded => MainServerReadOutcomeDto.Succeeded,
        MainServerReadOutcome.ActionRequired => MainServerReadOutcomeDto.ActionRequired,
        MainServerReadOutcome.OutcomeUnknown => MainServerReadOutcomeDto.OutcomeUnknown,
        _ => MainServerReadOutcomeDto.NotAttempted
    };

    private static MainServerReadOperationDto Map(MainServerReadOperation value) => value switch
    {
        MainServerReadOperation.BranchStatus => MainServerReadOperationDto.BranchStatus,
        MainServerReadOperation.InstalledBranch => MainServerReadOperationDto.InstalledBranch,
        _ => MainServerReadOperationDto.InstalledPos
    };
}
