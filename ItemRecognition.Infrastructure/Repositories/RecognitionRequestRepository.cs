using ItemRecognition.Application.Ports;
using ItemRecognition.Domain.Entities;
using ItemRecognition.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ItemRecognition.Infrastructure.Repositories;

public sealed class RecognitionRequestRepository(ItemRecognitionDbContext dbContext) : IRecognitionRequestRepository
{
    public Task<RecognitionRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.RecognitionRequests
            .FirstOrDefaultAsync(request => request.Id == id, cancellationToken);

    public Task AddAsync(RecognitionRequest request, CancellationToken cancellationToken = default) =>
        dbContext.RecognitionRequests.AddAsync(request, cancellationToken).AsTask();

    public void Update(RecognitionRequest request) => dbContext.RecognitionRequests.Update(request);
}
