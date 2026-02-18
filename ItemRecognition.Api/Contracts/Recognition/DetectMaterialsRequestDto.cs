namespace ItemRecognition.Api.Contracts.Recognition;

public sealed record DetectMaterialsRequestDto(IReadOnlyList<string> Items);
