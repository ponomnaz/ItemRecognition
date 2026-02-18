using FluentValidation;
using ItemRecognition.Api.Contracts.Errors;
using ItemRecognition.Api.Contracts.Recognition;
using ItemRecognition.Api.Validation;
using ItemRecognition.Application.UseCases.DetectMainObjects;
using ItemRecognition.Application.UseCases.DetectMaterials;
using Microsoft.AspNetCore.Mvc;

namespace ItemRecognition.Api.Controllers;

[ApiController]
[Route("api/recognition")]
public sealed class RecognitionController(
    IDetectMainObjectsUseCase detectMainObjectsUseCase,
    IDetectMaterialsUseCase detectMaterialsUseCase,
    IValidator<CreateRecognitionRequestDto> createRecognitionRequestValidator,
    IValidator<DetectMaterialsRequestDto> detectMaterialsRequestValidator)
    : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(CreateRecognitionResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponseDto), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ApiErrorResponseDto), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateRecognitionAsync(
        [FromBody] CreateRecognitionRequestDto request,
        CancellationToken cancellationToken)
    {
        var validationResult = await createRecognitionRequestValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var error = ApiErrorResponseFactory.FromValidationFailures(
                validationResult.Errors,
                HttpContext.TraceIdentifier);

            return BadRequest(error);
        }

        var result = await detectMainObjectsUseCase.ExecuteAsync(
            new DetectMainObjectsRequest(request.ImageUrl),
            cancellationToken);

        if (result.IsSuccess)
        {
            var response = new CreateRecognitionResponseDto(
                result.RequestId,
                result.Objects
                    .Select(obj => new PredictedObjectDto(obj.Name, obj.IsPrimary, obj.Confidence, obj.Rank))
                    .ToArray());

            return Ok(response);
        }

        return CreateFailureResult(result.ErrorCode, result.ErrorMessage);
    }

    [HttpPost("{id:guid}/materials")]
    [ProducesResponseType(typeof(DetectMaterialsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponseDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponseDto), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponseDto), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ApiErrorResponseDto), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DetectMaterialsAsync(
        [FromRoute] Guid id,
        [FromBody] DetectMaterialsRequestDto request,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            var invalidIdError = ApiErrorResponseFactory.Create(
                "validation_error",
                "Request id cannot be empty.",
                HttpContext.TraceIdentifier,
                [new ApiErrorItemDto("id", "Request id must be a non-empty GUID.")]);

            return BadRequest(invalidIdError);
        }

        var validationResult = await detectMaterialsRequestValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var error = ApiErrorResponseFactory.FromValidationFailures(
                validationResult.Errors,
                HttpContext.TraceIdentifier);

            return BadRequest(error);
        }

        var result = await detectMaterialsUseCase.ExecuteAsync(
            new DetectMaterialsRequest(id, request.Items),
            cancellationToken);

        if (result.IsSuccess)
        {
            var response = new DetectMaterialsResponseDto(
                result.RequestId,
                result.Items
                    .Select(item => new MaterialsItemDto(item.ItemName, item.Materials))
                    .ToArray());

            return Ok(response);
        }

        return CreateFailureResult(result.ErrorCode, result.ErrorMessage);
    }

    private ObjectResult CreateFailureResult(string? code, string? message)
    {
        var normalizedCode = string.IsNullOrWhiteSpace(code)
            ? "internal_error"
            : code.Trim();
        var normalizedMessage = string.IsNullOrWhiteSpace(message)
            ? "Unhandled request processing error."
            : message.Trim();

        var statusCode = normalizedCode switch
        {
            "validation_error" => StatusCodes.Status400BadRequest,
            "request_not_found" => StatusCodes.Status404NotFound,
            "invalid_request_status" => StatusCodes.Status409Conflict,
            "ai_detection_failed" => StatusCodes.Status502BadGateway,
            "ai_call_missing" => StatusCodes.Status502BadGateway,
            "ai_empty_predictions" => StatusCodes.Status502BadGateway,
            "ai_http_error" => StatusCodes.Status502BadGateway,
            "ai_timeout" => StatusCodes.Status502BadGateway,
            "ai_transport_error" => StatusCodes.Status502BadGateway,
            "ai_invalid_response" => StatusCodes.Status502BadGateway,
            "ai_client_error" => StatusCodes.Status502BadGateway,
            _ => normalizedCode.StartsWith("ai_", StringComparison.OrdinalIgnoreCase)
                ? StatusCodes.Status502BadGateway
                : StatusCodes.Status500InternalServerError
        };

        var error = ApiErrorResponseFactory.Create(
            normalizedCode,
            normalizedMessage,
            HttpContext.TraceIdentifier);

        return StatusCode(statusCode, error);
    }
}
