using ItemRecognition.Application.Ports;
using ItemRecognition.Domain.Entities;
using ItemRecognition.Infrastructure.Persistence;

namespace ItemRecognition.Infrastructure.Repositories;

public sealed class ConfirmedObjectMaterialRepository(ItemRecognitionDbContext dbContext)
    : IConfirmedObjectMaterialRepository
{
    public Task AddRangeAsync(
        IReadOnlyCollection<ConfirmedObjectMaterial> links,
        CancellationToken cancellationToken = default) =>
        dbContext.ConfirmedObjectMaterials.AddRangeAsync(links, cancellationToken);
}
