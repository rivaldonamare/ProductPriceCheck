namespace ElectricalStoreAI.Domain.Products;

public sealed class ProductReferenceImage
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    public string StorageKey { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
}
