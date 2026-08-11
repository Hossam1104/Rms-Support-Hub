using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Domain.Interfaces;

/// <summary>
/// Optional, non-destructive post-restore verification seam. Keeping it separate from
/// <see cref="IDatabaseService"/> preserves existing disposable database fakes while allowing the
/// Windows SQL adapter and focused tests to prove the post-check behavior.
/// </summary>
public interface IDatabaseRestoreVerifier
{
    Task<bool> VerifyRestoreAsync(
        AppSettings settings,
        string targetDatabase,
        CancellationToken cancellationToken = default);
}
