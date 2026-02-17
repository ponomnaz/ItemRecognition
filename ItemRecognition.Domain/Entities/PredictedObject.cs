namespace ItemRecognition.Domain.Entities;

public class PredictedObject
{
    public Guid Id { get; set; }

    public Guid RequestId { get; set; }
    public RecognitionRequest? Request { get; set; }

    public Guid AiCallId { get; set; }
    public AiCall? AiCall { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string Name { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public float? Confidence { get; set; }
    public int Rank { get; set; }
}
