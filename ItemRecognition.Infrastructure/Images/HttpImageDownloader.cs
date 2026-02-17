using System.Net.Http.Headers;
using ItemRecognition.Application.Common.Images;
using ItemRecognition.Application.Ports;

namespace ItemRecognition.Infrastructure.Images;

public sealed class HttpImageDownloader(HttpClient httpClient, ImageProcessingOptions options) : IImageDownloader
{
    private readonly HashSet<string> _allowedContentTypes =
        new(options.AllowedContentTypes, StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<string> _allowedSchemes =
        new(options.AllowedSchemes, StringComparer.OrdinalIgnoreCase);

    public async Task<DownloadedImage> DownloadAsync(
        Uri imageUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imageUrl);

        if (!imageUrl.IsAbsoluteUri)
        {
            throw new ArgumentException("Image URL must be absolute.", nameof(imageUrl));
        }

        if (!_allowedSchemes.Contains(imageUrl.Scheme))
        {
            throw new ArgumentException("Unsupported URL scheme.", nameof(imageUrl));
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, imageUrl);
        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var contentType = GetContentType(response.Content.Headers);
        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new InvalidOperationException("Image content type is missing.");
        }

        if (!_allowedContentTypes.Contains(contentType))
        {
            throw new InvalidOperationException($"Unsupported content type: {contentType}.");
        }

        var contentLength = response.Content.Headers.ContentLength;
        if (contentLength.HasValue && contentLength.Value > options.MaxBytes)
        {
            throw new InvalidOperationException("Image is too large.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var buffer = new byte[81920];
        long total = 0;

        using var memory = new MemoryStream();
        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            total += read;
            if (total > options.MaxBytes)
            {
                throw new InvalidOperationException("Image is too large.");
            }

            await memory.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        return total == 0 ? throw new InvalidOperationException("Image content is empty.") : new DownloadedImage(memory.ToArray(), contentType, total);
    }

    private static string? GetContentType(HttpContentHeaders headers)
    {
        if (headers.ContentType?.MediaType is { Length: > 0 } mediaType)
        {
            return mediaType;
        }

        return null;
    }
}
