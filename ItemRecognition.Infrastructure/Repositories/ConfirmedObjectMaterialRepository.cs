using ItemRecognition.Application.Ports;
using ItemRecognition.Domain.Entities;
using ItemRecognition.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ItemRecognition.Infrastructure.Repositories;

public sealed class ConfirmedObjectMaterialRepository(ItemRecognitionDbContext dbContext)
    : IConfirmedObjectMaterialRepository
{
    public async Task<IReadOnlyList<ConfirmedObjectMaterial>> GetByConfirmedObjectIdsAsync(
        IReadOnlyCollection<Guid> confirmedObjectIds,
        CancellationToken cancellationToken = default)
    {
        if (confirmedObjectIds.Count == 0)
        {
            return [];
        }

        return await dbContext.ConfirmedObjectMaterials
            .Where(link => confirmedObjectIds.Contains(link.ConfirmedObjectId))
            .ToListAsync(cancellationToken);
    }

    public Task AddRangeAsync(
        IReadOnlyCollection<ConfirmedObjectMaterial> links,
        CancellationToken cancellationToken = default) =>
        dbContext.ConfirmedObjectMaterials.AddRangeAsync(links, cancellationToken);
}
