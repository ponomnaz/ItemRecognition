using ItemRecognition.Domain.Entities;

namespace ItemRecognition.Application.Ports;

public interface IRecognitionRequestRepository
{
    Task<RecognitionRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(RecognitionRequest request, CancellationToken cancellationToken = default);
    void Update(RecognitionRequest request);
}
