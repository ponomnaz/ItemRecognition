using ItemRecognition.Application.Common.Reporting;

namespace ItemRecognition.Application.UseCases.GetAnonymizedExport;

public sealed record GetAnonymizedExportResponse(IReadOnlyList<AnonymizedExportRecord> Items);
