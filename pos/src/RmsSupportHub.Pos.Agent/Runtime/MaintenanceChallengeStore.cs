using RmsSupportHub.Pos.Agent;
using RmsSupportHub.Pos.Application.Maintenance;

namespace RmsSupportHub.Pos.Agent.Runtime;

public enum MaintenanceChallengeFailure
{
    None,
    NotFound,
    Expired,
    Used,
    WrongPrincipal,
    Changed
}

public sealed record MaintenanceChallenge(
    string ChallengeId,
    string PrincipalSid,
    MaintenancePreviewIntent Intent,
    DateTimeOffset ExpiresAtUtc,
    bool Used);

/// <summary>
/// Process-local, bounded preview intent store. The challenge is principal-bound and one-use. A
/// wrong principal cannot consume it; a matching principal consumes it even when the confirmation
/// or preview fingerprint is wrong, preventing repeated guessing against a destructive intent.
/// </summary>
public sealed class MaintenanceChallengeStore(
    TimeProvider clock,
    RuntimeRetentionPolicy retention)
{
    private readonly object gate = new();
    private readonly Dictionary<string, MaintenanceChallenge> entries = new(StringComparer.Ordinal);

    public MaintenanceChallenge Issue(string principalSid, MaintenancePreviewIntent intent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(principalSid);
        ArgumentNullException.ThrowIfNull(intent);

        var now = clock.GetUtcNow();
        var challenge = new MaintenanceChallenge(
            Guid.NewGuid().ToString("N"),
            principalSid,
            intent,
            now.Add(retention.MaintenanceChallengeLifetime),
            false);

        lock (gate)
        {
            PruneLocked(now);
            if (entries.Count >= retention.MaxMaintenanceChallenges)
            {
                throw new MaintenanceChallengeCapacityException();
            }

            entries.Add(challenge.ChallengeId, challenge);
        }

        return challenge;
    }

    public bool TryGet(string principalSid, string challengeId, out MaintenanceChallenge? challenge, out MaintenanceChallengeFailure failure)
    {
        lock (gate)
        {
            PruneLocked(clock.GetUtcNow());
            if (!entries.TryGetValue(challengeId, out var existing))
            {
                challenge = null;
                failure = MaintenanceChallengeFailure.NotFound;
                return false;
            }

            if (!string.Equals(existing.PrincipalSid, principalSid, StringComparison.Ordinal))
            {
                challenge = null;
                failure = MaintenanceChallengeFailure.WrongPrincipal;
                return false;
            }

            challenge = existing;
            failure = existing.Used
                ? MaintenanceChallengeFailure.Used
                : clock.GetUtcNow() >= existing.ExpiresAtUtc
                    ? MaintenanceChallengeFailure.Expired
                    : MaintenanceChallengeFailure.None;
            return failure == MaintenanceChallengeFailure.None;
        }
    }

    public bool TryConsume(
        string principalSid,
        string challengeId,
        string mode,
        string fingerprint,
        string typedConfirmation,
        out MaintenanceChallenge? challenge,
        out MaintenanceChallengeFailure failure)
    {
        lock (gate)
        {
            PruneLocked(clock.GetUtcNow());
            if (!entries.TryGetValue(challengeId, out var existing))
            {
                challenge = null;
                failure = MaintenanceChallengeFailure.NotFound;
                return false;
            }

            if (!string.Equals(existing.PrincipalSid, principalSid, StringComparison.Ordinal))
            {
                challenge = null;
                failure = MaintenanceChallengeFailure.WrongPrincipal;
                return false;
            }

            challenge = existing;
            if (existing.Used)
            {
                failure = MaintenanceChallengeFailure.Used;
                return false;
            }

            if (clock.GetUtcNow() >= existing.ExpiresAtUtc)
            {
                failure = MaintenanceChallengeFailure.Expired;
                return false;
            }

            var expectedMode = existing.Intent.Mode switch
            {
                MaintenanceMode.Cleanup => "cleanup",
                MaintenanceMode.BranchReset => "branch-reset",
                _ => ""
            };
            var valid = string.Equals(expectedMode, mode, StringComparison.Ordinal)
                && string.Equals(existing.Intent.Fingerprint, fingerprint, StringComparison.Ordinal)
                && string.Equals(existing.Intent.ConfirmationText, typedConfirmation, StringComparison.Ordinal);
            entries[challengeId] = existing with { Used = true };
            failure = valid ? MaintenanceChallengeFailure.None : MaintenanceChallengeFailure.Changed;
            return valid;
        }
    }

    private void PruneLocked(DateTimeOffset now)
    {
        foreach (var pair in entries.ToArray())
        {
            // Retain expired challenges for one additional challenge lifetime so callers receive
            // an explicit expired result rather than an indistinguishable not-found response.
            // They remain unusable and are still bounded by the normal retention cap.
            if (now - pair.Value.ExpiresAtUtc >= retention.MaintenanceChallengeLifetime)
            {
                entries.Remove(pair.Key);
            }
        }
    }
}

public sealed class MaintenanceChallengeCapacityException()
    : InvalidOperationException("The Agent maintenance challenge retention limit has been reached.");
