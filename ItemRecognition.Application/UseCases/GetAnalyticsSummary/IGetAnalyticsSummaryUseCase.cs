namespace ItemRecognition.Application.UseCases.GetAnalyticsSummary;

public interface IGetAnalyticsSummaryUseCase
{
    Task<GetAnalyticsSummaryResponse> ExecuteAsync(CancellationToken cancellationToken = default);
}
