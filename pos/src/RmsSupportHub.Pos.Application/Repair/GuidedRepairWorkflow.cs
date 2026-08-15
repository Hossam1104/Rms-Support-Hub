namespace RmsSupportHub.Pos.Application.Repair;

/// <summary>Fixed, server-owned Guided Repair checkpoint sequence.</summary>
public static class GuidedRepairWorkflow
{
    public static IReadOnlyList<GuidedRepairCheckpoint> Checkpoints { get; } =
    [
        new("snapshot-verified", "Verify safety snapshot", "Use a fresh, integrity-verified snapshot bound to this device and principal."),
        new("package-verified", "Verify package", "Verify the server-owned package signature, checksum, compatibility, and exact files."),
        new("capacity-verified", "Verify capacity", "Confirm the fixed Agent installation boundary has enough safe capacity."),
        new("preview-confirmed", "Confirm repair preview", "Confirm the exact typed repair plan shown by the Agent."),
        new("stage-package", "Stage package", "Stage the verified package inside the Agent installation boundary."),
        new("activate-package", "Activate package", "Activate only the exact Agent package and owned service registration."),
        new("health-verified", "Verify health", "Verify service startup, certificate access, ACLs, and Agent health after activation.")
    ];

    public static bool IsValidTransition(string? currentStepId, string requestedStepId)
    {
        var index = Array.FindIndex(Checkpoints.ToArray(), checkpoint => string.Equals(checkpoint.StepId, requestedStepId, StringComparison.Ordinal));
        if (index < 0) return false;
        if (currentStepId is null) return index == 0;
        var currentIndex = Array.FindIndex(Checkpoints.ToArray(), checkpoint => string.Equals(checkpoint.StepId, currentStepId, StringComparison.Ordinal));
        return currentIndex >= 0 && index == currentIndex + 1;
    }
}

public sealed record GuidedRepairCheckpoint(string StepId, string Title, string Description);
