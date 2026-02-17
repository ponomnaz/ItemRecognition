using ItemRecognition.Domain.Enums;

namespace ItemRecognition.Domain.Entities;

public class AiCall
{
    public Guid Id { get; set; }

    public Guid RequestId { get; set; }
    public RecognitionRequest? Request { get; set; }

    public AiStage Stage { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string PromptVersion { get; set; } = string.Empty;
    public string PromptText { get; set; } = string.Empty;

    public string RequestPayload { get; set; } = "{}";
    public string ResponseJson { get; set; } = "{}";

    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public int DurationMs { get; set; }

    public ICollection<PredictedObject> PredictedObjects { get; set; } = new List<PredictedObject>();
    public ICollection<ConfirmedObject> ConfirmedObjects { get; set; } = new List<ConfirmedObject>();
}
