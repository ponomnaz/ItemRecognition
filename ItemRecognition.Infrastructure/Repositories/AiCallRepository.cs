using ItemRecognition.Application.Ports;
using ItemRecognition.Domain.Entities;
using ItemRecognition.Domain.Enums;
using ItemRecognition.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ItemRecognition.Infrastructure.Repositories;

public sealed class AiCallRepository(ItemRecognitionDbContext dbContext) : IAiCallRepository
{
    public Task AddAsync(AiCall aiCall, CancellationToken cancellationToken = default) =>
        dbContext.AiCalls.AddAsync(aiCall, cancellationToken).AsTask();

    public Task<AiCall?> GetLatestByRequestAndStageAsync(
        Guid requestId,
        AiStage stage,
        CancellationToken cancellationToken = default) =>
        dbContext.AiCalls
            .Where(call => call.RequestId == requestId && call.Stage == stage)
            .OrderByDescending(call => call.CreatedAt)
            .ThenByDescending(call => call.Id)
            .FirstOrDefaultAsync(cancellationToken);
}
