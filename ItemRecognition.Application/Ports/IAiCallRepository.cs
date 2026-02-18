using ItemRecognition.Domain.Entities;
using ItemRecognition.Domain.Enums;

namespace ItemRecognition.Application.Ports;

public interface IAiCallRepository
{
    Task AddAsync(AiCall aiCall, CancellationToken cancellationToken = default);
    Task<AiCall?> GetLatestByRequestAndStageAsync(
        Guid requestId,
        AiStage stage,
        CancellationToken cancellationToken = default);
}
