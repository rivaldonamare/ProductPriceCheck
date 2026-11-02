using ElectricalStoreAI.Domain.Common;

namespace ElectricalStoreAI.Domain.Products;

public sealed class Product
{
    public Guid Id { get; set; }

    public string Sku { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Brand { get; set; }

    public string? Category { get; set; }

    public string? Description { get; set; }

    public decimal SalePrice { get; set; }

    public string SaleUnit { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Sku))
        {
            throw new DomainException("The product SKU is required.");
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new DomainException("The product name is required.");
        }

        if (string.IsNullOrWhiteSpace(SaleUnit))
        {
            throw new DomainException("The sale unit is required. Valid values include Pcs, Meter, Roll, and Pack.");
        }

        if (SalePrice < 0)
        {
            throw new DomainException("The sale price cannot be negative.");
        }
    }

    public static Product Create(
        string sku,
        string name,
        string? brand,
        string? category,
        string? description,
        decimal salePrice,
        string saleUnit,
        bool isActive = true)
    {
        var now = DateTime.UtcNow;

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Sku = sku,
            Name = name,
            Brand = brand,
            Category = category,
            Description = description,
            SalePrice = salePrice,
            SaleUnit = saleUnit,
            IsActive = isActive,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        product.Validate();

        return product;
    }
}
