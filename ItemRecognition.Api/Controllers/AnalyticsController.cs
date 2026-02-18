using ItemRecognition.Api.Contracts.Analytics;
using ItemRecognition.Api.Contracts.Errors;
using ItemRecognition.Api.Validation;
using ItemRecognition.Application.UseCases.GetAnalyticsSummary;
using Microsoft.AspNetCore.Mvc;

namespace ItemRecognition.Api.Controllers;

[ApiController]
[Route("api/analytics")]
public sealed class AnalyticsController(IGetAnalyticsSummaryUseCase getAnalyticsSummaryUseCase) : ControllerBase
{
    [HttpGet("summary")]
    [ProducesResponseType(typeof(AnalyticsSummaryResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponseDto), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetSummaryAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await getAnalyticsSummaryUseCase.ExecuteAsync(cancellationToken);
            var summary = result.Summary;

            var response = new AnalyticsSummaryResponseDto(
                summary.TotalRequests,
                summary.MainPipelineCompletedRequests,
                summary.MaterialsDetectedRequests,
                summary.FailedRequests,
                summary.RequestFailureRatePercent,
                summary.TotalAiCalls,
                summary.FailedAiCalls,
                summary.AiFailureRatePercent,
                summary.AverageMainStageDurationMs,
                summary.AverageMaterialsStageDurationMs,
                summary.AverageAiCallDurationMs,
                summary.TopObjects
                    .Select(item => new NamedCountDto(item.Name, item.Count))
                    .ToArray(),
                summary.TopMaterials
                    .Select(item => new NamedCountDto(item.Name, item.Count))
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
