using ItemRecognition.Domain.Entities;

namespace ItemRecognition.Application.Ports;

public interface IConfirmedObjectRepository
{
    Task AddRangeAsync(IReadOnlyCollection<ConfirmedObject> objects, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ConfirmedObject>> GetByRequestIdAsync(
        Guid requestId,
        CancellationToken cancellationToken = default);
}
