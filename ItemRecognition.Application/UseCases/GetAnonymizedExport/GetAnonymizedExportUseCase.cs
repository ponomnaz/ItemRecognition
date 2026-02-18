using ItemRecognition.Application.Ports;

namespace ItemRecognition.Application.UseCases.GetAnonymizedExport;

public sealed class GetAnonymizedExportUseCase(IAnonymizedExportQueryService queryService)
    : IGetAnonymizedExportUseCase
{
    public async Task<GetAnonymizedExportResponse> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var items = await queryService.GetAsync(cancellationToken);
        return new GetAnonymizedExportResponse(items);
    }
}
