using ItemRecognition.Application.Common.Reporting;
using ItemRecognition.Application.Ports;
using ItemRecognition.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ItemRecognition.Infrastructure.Reports;

public sealed class AnonymizedExportQueryService(ItemRecognitionDbContext dbContext) : IAnonymizedExportQueryService
{
    public async Task<IReadOnlyList<AnonymizedExportRecord>> GetAsync(CancellationToken cancellationToken = default)
    {
        var requests = await dbContext.RecognitionRequests
            .AsNoTracking()
            .OrderByDescending(request => request.CreatedAt)
            .Select(request => new RequestExportProjection(
                request.Id,
                request.CreatedAt,
                request.Status,
                request.ImageHash))
            .ToListAsync(cancellationToken);

        if (requests.Count == 0)
        {
            return Array.Empty<AnonymizedExportRecord>();
        }

        var requestIds = requests.Select(request => request.Id).ToArray();

        var predictedRows = await dbContext.PredictedObjects
            .AsNoTracking()
            .Where(obj => requestIds.AsEnumerable().Contains(obj.RequestId))
            .OrderBy(obj => obj.RequestId)
            .ThenBy(obj => obj.Rank)
            .Select(obj => new PredictedProjection(obj.RequestId, obj.Name))
            .ToListAsync(cancellationToken);

        var predictedByRequest = predictedRows
            .GroupBy(row => row.RequestId)
            .ToDictionary(
                group => group.Key, IReadOnlyList<string> (group) => group
                    .Select(row => row.Name)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray());

        var confirmedRows = await dbContext.ConfirmedObjects
            .AsNoTracking()
            .Where(obj => requestIds.AsEnumerable().Contains(obj.RequestId))
            .OrderBy(obj => obj.RequestId)
            .ThenBy(obj => obj.Name)
            .Select(obj => new ConfirmedProjection(obj.Id, obj.RequestId, obj.Name))
            .ToListAsync(cancellationToken);

        var confirmedIds = confirmedRows.Select(row => row.Id).ToArray();

        var materialRows = await dbContext.ConfirmedObjectMaterials
            .AsNoTracking()
            .Where(link => confirmedIds.AsEnumerable().Contains(link.ConfirmedObjectId))
            .Join(
                dbContext.Materials.AsNoTracking(),
                link => link.MaterialId,
                material => material.Id,
                (link, material) => new ConfirmedMaterialProjection(link.ConfirmedObjectId, material.Name))
            .ToListAsync(cancellationToken);

        var materialNamesByConfirmedObjectId = materialRows
            .GroupBy(row => row.ConfirmedObjectId)
            .ToDictionary(
                group => group.Key, IReadOnlyList<string> (group) => group
                    .Select(row => row.MaterialName)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToArray());

        var confirmedByRequest = confirmedRows
            .GroupBy(row => row.RequestId)
            .ToDictionary(
                group => group.Key, IReadOnlyList<AnonymizedConfirmedObjectRecord> (group) => group
                    .Select(row =>
                    {
                        var materials = materialNamesByConfirmedObjectId.TryGetValue(row.Id, out var materialNames)
                            ? materialNames
                            : [];

                        return new AnonymizedConfirmedObjectRecord(row.Name, materials);
                    })
                    .ToArray());

        return requests
            .Select(request => new AnonymizedExportRecord(
                request.Id,
                request.CreatedAt,
                request.Status,
                request.ImageHash,
                predictedByRequest.TryGetValue(request.Id, out var predictedObjects)
                    ? predictedObjects
                    : [],
                confirmedByRequest.TryGetValue(request.Id, out var confirmedObjects)
                    ? confirmedObjects
                    : []))
            .ToArray();
    }

    private sealed record RequestExportProjection(
        Guid Id,
        DateTimeOffset CreatedAt,
        Domain.Enums.RequestStatus Status,
        string? ImageHash);

    private sealed record PredictedProjection(Guid RequestId, string Name);

    private sealed record ConfirmedProjection(Guid Id, Guid RequestId, string Name);

    private sealed record ConfirmedMaterialProjection(Guid ConfirmedObjectId, string MaterialName);
}
