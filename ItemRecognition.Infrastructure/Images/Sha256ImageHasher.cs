using System.Security.Cryptography;
using ItemRecognition.Application.Ports;

namespace ItemRecognition.Infrastructure.Images;

public sealed class Sha256ImageHasher : IImageHasher
{
    public string ComputeSha256Hex(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var hash = SHA256.HashData(data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
