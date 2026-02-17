using ItemRecognition.Application.Ports;
using ItemRecognition.Domain.Entities;
using ItemRecognition.Infrastructure.Persistence;

namespace ItemRecognition.Infrastructure.Repositories;

public sealed class PredictedObjectRepository(ItemRecognitionDbContext dbContext) : IPredictedObjectRepository
{
    public Task AddRangeAsync(
        IReadOnlyCollection<PredictedObject> objects,
        CancellationToken cancellationToken = default) =>
        dbContext.PredictedObjects.AddRangeAsync(objects, cancellationToken);
}
