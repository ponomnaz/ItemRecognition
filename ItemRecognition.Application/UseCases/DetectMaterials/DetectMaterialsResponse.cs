using ItemRecognition.Application.Common.Ai;
using ItemRecognition.Domain.Enums;

namespace ItemRecognition.Application.UseCases.DetectMaterials;

public sealed record DetectMaterialsResponse(
    Guid RequestId,
    RequestStatus Status,
    IReadOnlyList<MaterialsPrediction> Items,
    string? ErrorCode,
    string? ErrorMessage)
{
    public bool IsSuccess => Status == RequestStatus.MaterialsDetected;
}
