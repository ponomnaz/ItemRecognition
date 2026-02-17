using ItemRecognition.Application.Common.Images;

namespace ItemRecognition.Application.Ports;

public interface IImageStorage
{
    Task<ImageStorageResult> SaveAsync(DownloadedImage image, CancellationToken cancellationToken = default);
}
