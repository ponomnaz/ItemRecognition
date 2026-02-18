using ItemRecognition.Api.Contracts.Errors;
using ItemRecognition.Api.Contracts.Exports;
using ItemRecognition.Api.Validation;
using ItemRecognition.Application.UseCases.GetAnonymizedExport;
using Microsoft.AspNetCore.Mvc;

namespace ItemRecognition.Api.Controllers;

[ApiController]
[Route("api/exports")]
public sealed class ExportsController(IGetAnonymizedExportUseCase getAnonymizedExportUseCase) : ControllerBase
{
    [HttpGet("anonymized")]
    [ProducesResponseType(typeof(AnonymizedExportResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponseDto), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAnonymizedAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await getAnonymizedExportUseCase.ExecuteAsync(cancellationToken);

            var response = new AnonymizedExportResponseDto(
                result.Items
                    .Select(item => new AnonymizedExportItemDto(
                        item.RequestId,
                        item.CreatedAt,
                        item.Status.ToString(),
                        item.ImageHash,
                        item.PredictedObjects,
                        item.ConfirmedObjects
                            .Select(confirmedObject => new AnonymizedConfirmedObjectDto(
                                confirmedObject.Name,
                                confirmedObject.Materials))
                            .ToArray()))
                    .ToArray());

            return Ok(response);
        }
        catch (Exception ex)
        {
            var error = ApiErrorResponseFactory.Create(
                "internal_error",
                ex.Message,
                HttpContext.TraceIdentifier);

            return StatusCode(StatusCodes.Status500InternalServerError, error);
        }
    }
}
