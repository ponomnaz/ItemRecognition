namespace ItemRecognition.Api.Contracts.Recognition;

public sealed record MaterialsItemDto(
    string Name,
    IReadOnlyList<string> Materials);
