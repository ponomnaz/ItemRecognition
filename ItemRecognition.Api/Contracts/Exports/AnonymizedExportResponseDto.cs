namespace ItemRecognition.Api.Contracts.Exports;

public sealed record AnonymizedExportResponseDto(IReadOnlyList<AnonymizedExportItemDto> Items);

public sealed record AnonymizedExportItemDto(
    Guid RequestId,
    DateTimeOffset CreatedAt,
    string Status,
    string? ImageHash,
    IReadOnlyList<string> PredictedObjects,
    IReadOnlyList<AnonymizedConfirmedObjectDto> ConfirmedObjects);

public sealed record AnonymizedConfirmedObjectDto(
    string Name,
    IReadOnlyList<string> Materials);
