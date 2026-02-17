using ItemRecognition.Application.Ports;
using ItemRecognition.Domain.Entities;
using ItemRecognition.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ItemRecognition.Infrastructure.Repositories;

public sealed class ConfirmedObjectRepository(ItemRecognitionDbContext dbContext) : IConfirmedObjectRepository
{
    public Task AddRangeAsync(
        IReadOnlyCollection<ConfirmedObject> objects,
        CancellationToken cancellationToken = default) =>
        dbContext.ConfirmedObjects.AddRangeAsync(objects, cancellationToken);

    public async Task<IReadOnlyList<ConfirmedObject>> GetByRequestIdAsync(
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.ConfirmedObjects
            .Where(obj => obj.RequestId == requestId)
            .ToListAsync(cancellationToken);
    }
}
