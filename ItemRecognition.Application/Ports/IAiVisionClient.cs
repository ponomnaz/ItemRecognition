using ItemRecognition.Application.Common.Ai;
using ItemRecognition.Application.Common.Images;

namespace ItemRecognition.Application.Ports;

public interface IAiVisionClient
{
    Task<AiResult<IReadOnlyList<MainObjectPrediction>>> DetectMainObjectsAsync(
        Guid requestId,
        DownloadedImage image,
        string promptVersion,
        string promptText,
        CancellationToken cancellationToken = default);

    Task<AiResult<IReadOnlyList<MaterialsPrediction>>> DetectMaterialsAsync(
        Guid requestId,
        DownloadedImage image,
        IReadOnlyList<string> itemNames,
        string promptVersion,
        string promptText,
        CancellationToken cancellationToken = default);
}
