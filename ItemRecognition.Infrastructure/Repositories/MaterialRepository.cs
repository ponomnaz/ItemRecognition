using ItemRecognition.Application.Ports;
using ItemRecognition.Domain.Entities;
using ItemRecognition.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ItemRecognition.Infrastructure.Repositories;

public sealed class MaterialRepository(ItemRecognitionDbContext dbContext) : IMaterialRepository
{
    public async Task<IReadOnlyList<Material>> GetByNamesAsync(
        IReadOnlyCollection<string> names,
        CancellationToken cancellationToken = default)
    {
        if (names.Count == 0)
        {
            return Array.Empty<Material>();
        }

        return await dbContext.Materials
            .Where(material => names.Contains(material.Name))
            .ToListAsync(cancellationToken);
    }

    public Task AddRangeAsync(IReadOnlyCollection<Material> materials, CancellationToken cancellationToken = default)
    {
        if (materials.Count == 0)
        {
            return Task.CompletedTask;
        }

        return dbContext.Materials.AddRangeAsync(materials, cancellationToken);
    }
}
