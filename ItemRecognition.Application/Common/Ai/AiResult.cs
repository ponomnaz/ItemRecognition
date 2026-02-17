namespace ItemRecognition.Application.Common.Ai;

public sealed record AiResult<T>(T? Value, AiError? Error)
{
    public bool IsSuccess => Error is null;

    public static AiResult<T> Success(T value) => new(value, null);

    public static AiResult<T> Failure(string code, string message) =>
        new(default, new AiError(code, message));
}
