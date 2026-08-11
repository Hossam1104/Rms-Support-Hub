using RmsSupportHub.Pos.Domain.Enums;
using RmsSupportHub.Pos.Domain.Interfaces;
using RmsSupportHub.Pos.Domain.Models;

namespace RmsSupportHub.Pos.Application.UseCases;

public sealed class TestConnectionUseCase(IDatabaseService databaseService)
{
    public async Task<OperationResult> ExecuteAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        var result = OperationResult.Running("test_connection");

        try
        {
            await databaseService.TestConnectionAsync(settings, cancellationToken: cancellationToken).ConfigureAwait(false);
            result.AddMessage("Database connection test completed");
            result.Finalize(OperationStatus.Success);
            return result;
        }
        catch (Exception ex)
        {
            result.AddError($"Database connection test failed: {ex.Message}");
            result.Finalize(OperationStatus.Failed);
            return result;
        }
    }
}
