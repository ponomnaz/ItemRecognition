using ItemRecognition.Application.Ports;

namespace ItemRecognition.Application.UseCases.GetAnalyticsSummary;

public sealed class GetAnalyticsSummaryUseCase(IAnalyticsSummaryQueryService queryService)
    : IGetAnalyticsSummaryUseCase
{
    public async Task<GetAnalyticsSummaryResponse> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var summary = await queryService.GetAsync(cancellationToken);
        return new GetAnalyticsSummaryResponse(summary);
    }
}
