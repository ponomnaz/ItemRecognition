namespace ItemRecognition.Api.Contracts.Recognition;

public sealed record PredictedObjectDto(
    string Name,
    bool IsPrimary,
    float? Confidence,
    int Rank);
