using FluentValidation.Results;
using ItemRecognition.Api.Contracts.Errors;

namespace ItemRecognition.Api.Validation;

public static class ApiErrorResponseFactory
{
    public static ApiErrorResponseDto FromValidationFailures(
        IEnumerable<ValidationFailure> failures,
        string? traceId = null)
    {
        ArgumentNullException.ThrowIfNull(failures);

        var errors = failures
            .Select(failure => new ApiErrorItemDto(
                string.IsNullOrWhiteSpace(failure.PropertyName) ? "request" : failure.PropertyName,
                failure.ErrorMessage))
            .ToArray();

        return new ApiErrorResponseDto(
            "validation_error",
            "Request validation failed.",
            traceId,
            errors);
    }

    public static ApiErrorResponseDto Create(
        string code,
        string message,
        string? traceId = null,
        IReadOnlyList<ApiErrorItemDto>? errors = null)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Error code is required.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Error message is required.", nameof(message));
        }

        return new ApiErrorResponseDto(
            code.Trim(),
            message.Trim(),
            traceId,
            errors ?? Array.Empty<ApiErrorItemDto>());
    }
}
