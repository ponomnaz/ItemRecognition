using ItemRecognition.Application.Common.Images;
using ItemRecognition.Application.Ports;

namespace ItemRecognition.Infrastructure.Images;

public sealed class LocalImageStorage(ImageProcessingOptions options) : IImageStorage
{
    public async Task<ImageStorageResult> SaveAsync(
        DownloadedImage image,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);

        var root = ResolveStorageRoot();
        Directory.CreateDirectory(root);

        var extension = ImageContentTypeMap.ToFileExtension(image.ContentType);
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(root, fileName);

        await File.WriteAllBytesAsync(fullPath, image.Content, cancellationToken);

        return new ImageStorageResult(fileName, image.Content.Length, image.ContentType);
    }

    private string ResolveStorageRoot()
    {
        var root = options.StorageRoot;
        return Path.IsPathRooted(root) ? root : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, root));
    }
}
