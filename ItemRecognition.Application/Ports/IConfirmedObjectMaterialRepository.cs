using ItemRecognition.Domain.Entities;

namespace ItemRecognition.Application.Ports;

public interface IConfirmedObjectMaterialRepository
{
    Task<IReadOnlyList<ConfirmedObjectMaterial>> GetByConfirmedObjectIdsAsync(
        IReadOnlyCollection<Guid> confirmedObjectIds,
        CancellationToken cancellationToken = default);

    Task AddRangeAsync(
        IReadOnlyCollection<ConfirmedObjectMaterial> links,
        CancellationToken cancellationToken = default);
}
