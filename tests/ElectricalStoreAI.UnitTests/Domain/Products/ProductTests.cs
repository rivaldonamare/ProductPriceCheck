using ElectricalStoreAI.Domain.Common;
using ElectricalStoreAI.Domain.Products;

namespace ElectricalStoreAI.UnitTests.Domain.Products;

public sealed class ProductTests
{
    [Fact]
    public void Create_WithMissingSku_ShouldThrowDomainException()
    {
        var act = () => Product.Create(
            string.Empty,
            "LED Bulb 10W",
            "Brand",
            "Lighting",
            null,
            10000m,
            "Pcs");

        Assert.Throws<DomainException>(act);
    }

    [Fact]
    public void Create_WithMissingName_ShouldThrowDomainException()
    {
        var act = () => Product.Create(
            "SKU-LED-10",
            " ",
            null,
            null,
            null,
            10000m,
            "Pcs");

        Assert.Throws<DomainException>(act);
    }

    [Fact]
    public void Create_WithMissingSaleUnit_ShouldThrowDomainException()
    {
        var act = () => Product.Create(
            "SKU-LED-10",
            "LED Bulb 10W",
            null,
            null,
            null,
            10000m,
            string.Empty);

        Assert.Throws<DomainException>(act);
    }

    [Fact]
    public void Create_WithNegativeSalePrice_ShouldThrowDomainException()
    {
        var act = () => Product.Create(
            "SKU-LED-10",
            "LED Bulb 10W",
            null,
            null,
            null,
            -1m,
            "Pcs");

        Assert.Throws<DomainException>(act);
    }

    [Fact]
    public void Create_WithValidData_ShouldCreateActiveProduct()
    {
        var product = Product.Create(
            "SKU-LED-10",
            "LED Bulb 10W",
            "Brand",
            "Lighting",
            null,
            10000m,
            "Pcs",
            isActive: false);

        Assert.NotEqual(Guid.Empty, product.Id);
        Assert.Equal("SKU-LED-10", product.Sku);
        Assert.Equal("LED Bulb 10W", product.Name);
        Assert.False(product.IsActive);
        Assert.Equal(10000m, product.SalePrice);
    }
}
