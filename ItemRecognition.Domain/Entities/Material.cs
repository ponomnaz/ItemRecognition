namespace ItemRecognition.Domain.Entities;

public class Material
{
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public string Name { get; set; } = string.Empty;

    public ICollection<ConfirmedObjectMaterial> ConfirmedObjectMaterials { get; set; } =
        new List<ConfirmedObjectMaterial>();
}
