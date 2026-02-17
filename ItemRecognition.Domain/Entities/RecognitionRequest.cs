using ItemRecognition.Domain.Enums;

namespace ItemRecognition.Domain.Entities;

public class RecognitionRequest
{
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public RequestStatus Status { get; set; }

    public string ImageUrl { get; set; } = string.Empty;
    public string? ImageHash { get; set; }
    public string? ImageStorageKey { get; set; }

    public ICollection<AiCall> AiCalls { get; set; } = new List<AiCall>();
    public ICollection<PredictedObject> PredictedObjects { get; set; } = new List<PredictedObject>();
    public ICollection<ConfirmedObject> ConfirmedObjects { get; set; } = new List<ConfirmedObject>();
}
