using RmsSupportHub.Pos.Domain.Enums;

namespace RmsSupportHub.Pos.Domain.Models;

public sealed class OperationResult
{
    public OperationResult(string operationType)
    {
        OperationType = operationType;
    }

    public string OperationType { get; }

    public OperationStatus Status { get; private set; } = OperationStatus.Pending;

    public DateTimeOffset StartTime { get; } = DateTimeOffset.Now;

    public DateTimeOffset? EndTime { get; private set; }

    public bool Success { get; private set; }

    public List<string> Errors { get; } = [];

    public List<string> Messages { get; } = [];

    public Dictionary<string, object?> Context { get; } = [];

    public static OperationResult Running(string operationType)
    {
        var result = new OperationResult(operationType);
        result.Status = OperationStatus.Running;
        return result;
    }

    public void AddError(string error)
    {
        if (!string.IsNullOrWhiteSpace(error))
        {
            Errors.Add(error);
        }

        Success = false;
    }

    public void AddMessage(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            Messages.Add(message);
        }
    }

    public void Finalize(OperationStatus status)
    {
        Status = status;
        EndTime = DateTimeOffset.Now;
        Success = status is OperationStatus.Success or OperationStatus.PartialSuccess;
    }
}
