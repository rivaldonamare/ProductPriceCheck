namespace ElectricalStoreAI.Domain.Products;

public sealed class ProductImageEmbedding
{
    public Guid Id { get; set; }

    public Guid ProductReferenceImageId { get; set; }

    public string ModelKey { get; set; } = string.Empty;

    public float[] Vector { get; set; } = Array.Empty<float>();

    public DateTime CreatedAtUtc { get; set; }
}
