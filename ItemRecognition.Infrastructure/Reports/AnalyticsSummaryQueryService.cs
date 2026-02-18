using ItemRecognition.Application.Common.Reporting;
using ItemRecognition.Application.Ports;
using ItemRecognition.Domain.Enums;
using ItemRecognition.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ItemRecognition.Infrastructure.Reports;

public sealed class AnalyticsSummaryQueryService(ItemRecognitionDbContext dbContext) : IAnalyticsSummaryQueryService
{
    public async Task<AnalyticsSummaryRecord> GetAsync(CancellationToken cancellationToken = default)
    {
        var statusCounts = await dbContext.RecognitionRequests
            .AsNoTracking()
            .GroupBy(request => request.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var totalRequests = statusCounts.Sum(item => item.Count);
        var failedRequests = statusCounts.FirstOrDefault(item => item.Status == RequestStatus.Failed)?.Count ?? 0;
        var materialsDetectedRequests =
            statusCounts.FirstOrDefault(item => item.Status == RequestStatus.MaterialsDetected)?.Count ?? 0;

        var mainPipelineCompletedRequests = statusCounts
            .Where(item => item.Status is RequestStatus.MainDetected or RequestStatus.Confirmed or RequestStatus.MaterialsDetected)
            .Sum(item => item.Count);

        var requestFailureRate = totalRequests == 0 ? 0d : failedRequests * 100d / totalRequests;

        var totalAiCalls = await dbContext.AiCalls
            .AsNoTracking()
            .CountAsync(cancellationToken);

        var failedAiCalls = await dbContext.AiCalls
            .AsNoTracking()
            .CountAsync(call => !call.IsSuccess, cancellationToken);

        var aiFailureRate = totalAiCalls == 0 ? 0d : failedAiCalls * 100d / totalAiCalls;

        var mainAvgDuration = await dbContext.AiCalls
            .AsNoTracking()
            .Where(call => call.Stage == AiStage.MainObjects)
            .Select(call => (double?)call.DurationMs)
            .AverageAsync(cancellationToken);

        var materialsAvgDuration = await dbContext.AiCalls
            .AsNoTracking()
            .Where(call => call.Stage == AiStage.Materials)
            .Select(call => (double?)call.DurationMs)
            .AverageAsync(cancellationToken);

        var overallAvgDuration = await dbContext.AiCalls
            .AsNoTracking()
            .Select(call => (double?)call.DurationMs)
            .AverageAsync(cancellationToken);

        var topObjects = await dbContext.ConfirmedObjects
            .AsNoTracking()
            .GroupBy(obj => obj.Name.ToLower())
            .Select(group => new
            {
                Name = group.Min(obj => obj.Name) ?? string.Empty,
                Count = group.Count()
            })
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Name)
            .Take(10)
            .Select(item => new NamedCountRecord(item.Name, item.Count))
            .ToListAsync(cancellationToken);

        var topMaterials = await dbContext.ConfirmedObjectMaterials
            .AsNoTracking()
            .Join(
                dbContext.Materials.AsNoTracking(),
                link => link.MaterialId,
                material => material.Id,
                (_, material) => material.Name)
            .GroupBy(name => name.ToLower())
            .Select(group => new
            {
                Name = group.Min() ?? string.Empty,
                Count = group.Count()
            })
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Name)
            .Take(10)
            .Select(item => new NamedCountRecord(item.Name, item.Count))
            .ToListAsync(cancellationToken);

        return new AnalyticsSummaryRecord(
            totalRequests,
            mainPipelineCompletedRequests,
            materialsDetectedRequests,
            failedRequests,
            requestFailureRate,
            totalAiCalls,
            failedAiCalls,
            aiFailureRate,
            mainAvgDuration,
            materialsAvgDuration,
            overallAvgDuration,
            topObjects,
            topMaterials);
    }
}
