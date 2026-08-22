namespace RmsSupportHub.Pos.Application.Invocation;

public sealed record ApplicationError(string Code, string Message);

public sealed record ApplicationResult<T>(
    bool Succeeded,
    T? Value,
    ApplicationError? Error)
{
    public static ApplicationResult<T> Success(T value) => new(true, value, null);

    public static ApplicationResult<T> Failure(string code, string message) =>
        new(false, default, new ApplicationError(code, message));
}
