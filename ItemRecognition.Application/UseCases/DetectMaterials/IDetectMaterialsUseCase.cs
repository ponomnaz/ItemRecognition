namespace ItemRecognition.Application.UseCases.DetectMaterials;

public interface IDetectMaterialsUseCase
{
    Task<DetectMaterialsResponse> ExecuteAsync(
        DetectMaterialsRequest request,
        CancellationToken cancellationToken = default);
}
