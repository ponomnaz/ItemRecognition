namespace ItemRecognition.Api.Contracts.Errors;

public sealed record ApiErrorResponseDto(
    string Code,
    string Message,
    string? TraceId,
    IReadOnlyList<ApiErrorItemDto> Errors);

public sealed record ApiErrorItemDto(
    string Target,
    string Message);
