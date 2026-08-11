namespace RmsSupportHub.Pos.Contracts.V1.Operations;

/// <summary>A single sanitized progress/status event within an operation's timeline.</summary>
public sealed record OperationEventDto(DateTimeOffset AtUtc, string Stage, string Message);
