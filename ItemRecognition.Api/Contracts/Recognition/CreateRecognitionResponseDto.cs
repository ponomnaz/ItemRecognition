namespace ItemRecognition.Api.Contracts.Recognition;

public sealed record CreateRecognitionResponseDto(
    Guid RequestId,
    IReadOnlyList<PredictedObjectDto> Objects);
