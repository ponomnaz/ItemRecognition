namespace ItemRecognition.Infrastructure.Images;

public sealed class ImageProcessingOptions
{
    public long MaxBytes { get; init; } = 8 * 1024 * 1024;

    public string[] AllowedContentTypes { get; init; } =
        ["image/jpeg", "image/png", "image/webp"];

    public string[] AllowedSchemes { get; init; } = ["http", "https"];

    public string StorageRoot { get; init; } = "storage/images";
}
