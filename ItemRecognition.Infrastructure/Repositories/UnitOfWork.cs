using ItemRecognition.Application.Ports;
using ItemRecognition.Infrastructure.Persistence;

namespace ItemRecognition.Infrastructure.Repositories;

public sealed class UnitOfWork(ItemRecognitionDbContext dbContext) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
