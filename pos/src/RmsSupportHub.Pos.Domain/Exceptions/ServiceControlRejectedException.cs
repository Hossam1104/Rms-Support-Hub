namespace RmsSupportHub.Pos.Domain.Exceptions;

/// <summary>
/// Safe, authoritative SCM rejection classification. The original exception is never serialized
/// through the Agent contract.
/// </summary>
public sealed class ServiceControlRejectedException : Exception
{
    public ServiceControlRejectedException(string code, Exception? innerException = null)
        : base("The Windows service rejected the control request.", innerException)
    {
        Code = string.IsNullOrWhiteSpace(code) ? "service_control_rejected" : code;
    }

    public string Code { get; }
}
