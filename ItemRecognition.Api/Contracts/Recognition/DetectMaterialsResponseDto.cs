namespace ItemRecognition.Api.Contracts.Recognition;

public sealed record DetectMaterialsResponseDto(
    Guid RequestId,
    IReadOnlyList<MaterialsItemDto> Items);
