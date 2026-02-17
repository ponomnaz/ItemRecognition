using ItemRecognition.Domain.Entities;

namespace ItemRecognition.Application.Ports;

public interface IMaterialRepository
{
    Task<IReadOnlyList<Material>> GetByNamesAsync(
        IReadOnlyCollection<string> names,
        CancellationToken cancellationToken = default);

    Task AddRangeAsync(IReadOnlyCollection<Material> materials, CancellationToken cancellationToken = default);
}
