using ItemRecognition.Application.Common.Images;

namespace ItemRecognition.Application.Ports;

public interface IImageDownloader
{
    Task<DownloadedImage> DownloadAsync(Uri imageUrl, CancellationToken cancellationToken = default);
}
