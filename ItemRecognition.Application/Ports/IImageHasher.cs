namespace ItemRecognition.Application.Ports;

public interface IImageHasher
{
    string ComputeSha256Hex(byte[] data);
}
