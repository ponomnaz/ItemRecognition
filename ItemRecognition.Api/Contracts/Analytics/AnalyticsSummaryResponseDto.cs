namespace ItemRecognition.Api.Contracts.Analytics;

public sealed record AnalyticsSummaryResponseDto(
    int TotalRequests,
    int MainPipelineCompletedRequests,
    int MaterialsDetectedRequests,
    int FailedRequests,
    double RequestFailureRatePercent,
    int TotalAiCalls,
    int FailedAiCalls,
    double AiFailureRatePercent,
    double? AverageMainStageDurationMs,
    double? AverageMaterialsStageDurationMs,
    double? AverageAiCallDurationMs,
    IReadOnlyList<NamedCountDto> TopObjects,
    IReadOnlyList<NamedCountDto> TopMaterials);

public sealed record NamedCountDto(string Name, int Count);
