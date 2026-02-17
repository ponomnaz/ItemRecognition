namespace ItemRecognition.Application.Common.Ai;

public sealed record MaterialsPrediction(string ItemName, IReadOnlyList<string> Materials);
