namespace ItemRecognition.Application.UseCases.DetectMaterials;

public sealed record DetectMaterialsRequest(Guid RequestId, IReadOnlyList<string> ItemNames);
