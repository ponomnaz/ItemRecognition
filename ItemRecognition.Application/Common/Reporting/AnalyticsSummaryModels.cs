namespace ItemRecognition.Application.Common.Reporting;

public sealed record AnalyticsSummaryRecord(
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
    IReadOnlyList<NamedCountRecord> TopObjects,
    IReadOnlyList<NamedCountRecord> TopMaterials);

public sealed record NamedCountRecord(string Name, int Count);
