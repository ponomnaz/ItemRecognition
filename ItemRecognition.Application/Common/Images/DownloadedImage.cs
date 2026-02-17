namespace ItemRecognition.Application.Common.Images;

public sealed record DownloadedImage(byte[] Content, string ContentType, long ContentLength);
