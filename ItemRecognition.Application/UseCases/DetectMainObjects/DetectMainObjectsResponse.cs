using ItemRecognition.Application.Common.Ai;
using ItemRecognition.Domain.Enums;

namespace ItemRecognition.Application.UseCases.DetectMainObjects;

public sealed record DetectMainObjectsResponse(
    Guid RequestId,
    RequestStatus Status,
    IReadOnlyList<MainObjectPrediction> Objects,
    string? ErrorCode,
    string? ErrorMessage)
{
    public bool IsSuccess => Status == RequestStatus.MainDetected;
}
