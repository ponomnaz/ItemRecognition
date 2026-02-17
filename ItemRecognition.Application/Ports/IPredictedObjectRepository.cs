using ItemRecognition.Domain.Entities;

namespace ItemRecognition.Application.Ports;

public interface IPredictedObjectRepository
{
    Task AddRangeAsync(IReadOnlyCollection<PredictedObject> objects, CancellationToken cancellationToken = default);
}
