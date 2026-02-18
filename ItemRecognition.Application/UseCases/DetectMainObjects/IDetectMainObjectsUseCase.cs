namespace ItemRecognition.Application.UseCases.DetectMainObjects;

public interface IDetectMainObjectsUseCase
{
    Task<DetectMainObjectsResponse> ExecuteAsync(
        DetectMainObjectsRequest request,
        CancellationToken cancellationToken = default);
}
