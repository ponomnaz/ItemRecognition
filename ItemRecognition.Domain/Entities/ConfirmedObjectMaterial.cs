namespace ItemRecognition.Domain.Entities;

public class ConfirmedObjectMaterial
{
    public Guid ConfirmedObjectId { get; set; }
    public ConfirmedObject? ConfirmedObject { get; set; }

    public Guid MaterialId { get; set; }
    public Material? Material { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
