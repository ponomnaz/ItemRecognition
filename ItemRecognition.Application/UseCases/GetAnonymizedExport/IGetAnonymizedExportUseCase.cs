namespace ItemRecognition.Application.UseCases.GetAnonymizedExport;

public interface IGetAnonymizedExportUseCase
{
    Task<GetAnonymizedExportResponse> ExecuteAsync(CancellationToken cancellationToken = default);
}
