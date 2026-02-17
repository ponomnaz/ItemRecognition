using ItemRecognition.Domain.Entities;

namespace ItemRecognition.Application.Ports;

public interface IConfirmedObjectMaterialRepository
{
    Task AddRangeAsync(
        IReadOnlyCollection<ConfirmedObjectMaterial> links,
        CancellationToken cancellationToken = default);
}
