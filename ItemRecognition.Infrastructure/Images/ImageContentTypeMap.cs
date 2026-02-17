namespace ItemRecognition.Infrastructure.Images;

internal static class ImageContentTypeMap
{
    public static string ToFileExtension(string contentType)
    {
        return contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/jpg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => ".bin"
        };
    }
}
