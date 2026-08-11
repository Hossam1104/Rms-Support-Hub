using RmsSupportHub.Pos.Domain.Enums;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Application.Services;

public sealed class BranchVerificationService(IDatabaseService databaseService)
{
    public async Task<OperationResult> VerifyAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        var result = OperationResult.Running("verify_branch");

        var branchCode = settings.BranchCode?.Trim();
        if (string.IsNullOrWhiteSpace(branchCode))
        {
            return Fail(result, "Branch code is required");
        }

        try
        {
            var exists = await databaseService.BranchExistsAsync(settings, branchCode, cancellationToken).ConfigureAwait(false);
            result.AddMessage(exists ? "Branch exists" : "Branch not found");
            result.Finalize(exists ? OperationStatus.Success : OperationStatus.Failed);
            return result;
        }
        catch (Exception ex)
        {
            return Fail(result, $"Branch verification failed: {ex.Message}");
        }
    }

    private static OperationResult Fail(OperationResult result, string message)
    {
        result.AddError(message);
        result.Finalize(OperationStatus.Failed);
        return result;
    }
}
