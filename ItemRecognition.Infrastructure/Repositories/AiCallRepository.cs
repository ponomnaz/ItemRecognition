using ItemRecognition.Application.Ports;
using ItemRecognition.Domain.Entities;
using ItemRecognition.Infrastructure.Persistence;

namespace ItemRecognition.Infrastructure.Repositories;

public sealed class AiCallRepository(ItemRecognitionDbContext dbContext) : IAiCallRepository
{
    public Task AddAsync(AiCall aiCall, CancellationToken cancellationToken = default) =>
        dbContext.AiCalls.AddAsync(aiCall, cancellationToken).AsTask();
}
