using ItemRecognition.Domain.Entities;

namespace ItemRecognition.Application.Ports;

public interface IAiCallRepository
{
    Task AddAsync(AiCall aiCall, CancellationToken cancellationToken = default);
}
