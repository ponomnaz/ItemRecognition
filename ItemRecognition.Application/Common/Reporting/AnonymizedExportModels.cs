using ItemRecognition.Domain.Enums;

namespace ItemRecognition.Application.Common.Reporting;

public sealed record AnonymizedExportRecord(
    Guid RequestId,
    DateTimeOffset CreatedAt,
    RequestStatus Status,
    string? ImageHash,
    IReadOnlyList<string> PredictedObjects,
    IReadOnlyList<AnonymizedConfirmedObjectRecord> ConfirmedObjects);

public sealed record AnonymizedConfirmedObjectRecord(
    string Name,
    IReadOnlyList<string> Materials);
