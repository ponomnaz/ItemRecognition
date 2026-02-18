using ItemRecognition.Application.Common.Reporting;

namespace ItemRecognition.Application.Ports;

public interface IAnalyticsSummaryQueryService
{
    Task<AnalyticsSummaryRecord> GetAsync(CancellationToken cancellationToken = default);
}
