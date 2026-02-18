using ItemRecognition.Application.Common.Reporting;

namespace ItemRecognition.Application.Ports;

public interface IAnonymizedExportQueryService
{
    Task<IReadOnlyList<AnonymizedExportRecord>> GetAsync(CancellationToken cancellationToken = default);
}
