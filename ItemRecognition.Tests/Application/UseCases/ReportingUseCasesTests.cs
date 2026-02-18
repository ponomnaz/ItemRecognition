using ItemRecognition.Application.Common.Reporting;
using ItemRecognition.Application.Ports;
using ItemRecognition.Application.UseCases.GetAnalyticsSummary;
using ItemRecognition.Application.UseCases.GetAnonymizedExport;
using ItemRecognition.Domain.Enums;

namespace ItemRecognition.Tests.Application.UseCases;

public sealed class ReportingUseCasesTests
{
    [Fact]
    public async Task GetAnonymizedExportUseCase_ReturnsItemsFromQueryService()
    {
        var expected = new[]
        {
            new AnonymizedExportRecord(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                RequestStatus.MaterialsDetected,
                "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                ["стол"],
                [new AnonymizedConfirmedObjectRecord("стол", ["металл"])])
        };

        var useCase = new GetAnonymizedExportUseCase(new StubAnonymizedExportQueryService(expected));
        var result = await useCase.ExecuteAsync();

        Assert.Single(result.Items);
        Assert.Equal(expected[0].RequestId, result.Items[0].RequestId);
        Assert.Equal(expected[0].ImageHash, result.Items[0].ImageHash);
    }

    [Fact]
    public async Task GetAnalyticsSummaryUseCase_ReturnsSummaryFromQueryService()
    {
        var expected = new AnalyticsSummaryRecord(
            10,
            8,
            6,
            2,
            20.0,
            12,
            3,
            25.0,
            350.0,
            420.0,
            380.0,
            [new NamedCountRecord("стол", 5)],
            [new NamedCountRecord("металл", 8)]);

        var useCase = new GetAnalyticsSummaryUseCase(new StubAnalyticsSummaryQueryService(expected));
        var result = await useCase.ExecuteAsync();

        Assert.Equal(10, result.Summary.TotalRequests);
        Assert.Equal(20.0, result.Summary.RequestFailureRatePercent);
        Assert.Equal("металл", Assert.Single(result.Summary.TopMaterials).Name);
    }

    private sealed class StubAnonymizedExportQueryService(IReadOnlyList<AnonymizedExportRecord> items)
        : IAnonymizedExportQueryService
    {
        public Task<IReadOnlyList<AnonymizedExportRecord>> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(items);
    }

    private sealed class StubAnalyticsSummaryQueryService(AnalyticsSummaryRecord summary)
        : IAnalyticsSummaryQueryService
    {
        public Task<AnalyticsSummaryRecord> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(summary);
    }
}
