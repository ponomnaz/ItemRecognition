namespace ItemRecognition.Application.Common.Ai;

public sealed record MainObjectPrediction(string Name, bool IsPrimary, float? Confidence, int Rank);
