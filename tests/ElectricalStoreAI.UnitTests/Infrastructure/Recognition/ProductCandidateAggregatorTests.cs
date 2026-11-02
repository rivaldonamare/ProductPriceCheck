using ElectricalStoreAI.Application.Recognition;
using ElectricalStoreAI.Domain.Products;
using ElectricalStoreAI.Infrastructure.Recognition;

namespace ElectricalStoreAI.UnitTests.Infrastructure.Recognition;

public sealed class ProductCandidateAggregatorTests
{
    [Fact]
    public void Rank_ShouldReturnDistinctProductsOrderedBySimilarity()
    {
        var candidates = new List<ScoredProduct>
        {
            new(new Product { Id = Guid.NewGuid(), Sku = "SKU-A", Name = "Product A", SalePrice = 1000m, SaleUnit = "Pcs" }, new float[] { 1f }, 0.9),
            new(new Product { Id = Guid.NewGuid(), Sku = "SKU-B", Name = "Product B", SalePrice = 2000m, SaleUnit = "Pcs" }, new float[] { 0.8f }, 0.8),
        };

        var result = ProductCandidateAggregator.Rank(candidates);

        Assert.Equal(2, result.Count);
        Assert.Equal("SKU-A", result[0].Sku);
        Assert.Equal("SKU-B", result[1].Sku);
    }

    [Fact]
    public void Rank_ShouldAggregateMultipleImagesPerProductIntoSingleCandidate()
    {
        var productId = Guid.NewGuid();

        var candidates = new List<ScoredProduct>
        {
            new(new Product { Id = productId, Sku = "SKU-A", Name = "Product A", SalePrice = 1000m, SaleUnit = "Pcs" }, new float[] { 0.7f }, 0.5),
            new(new Product { Id = productId, Sku = "SKU-A", Name = "Product A", SalePrice = 1000m, SaleUnit = "Pcs" }, new float[] { 0.9f }, 0.9),
        };

        var result = ProductCandidateAggregator.Rank(candidates);

        Assert.Single(result);
        Assert.Equal(productId, result[0].ProductId);
        Assert.Equal(0.9, result[0].SimilarityScore);
    }

    [Fact]
    public void Rank_ShouldReturnAtMostTopFiveCandidates()
    {
        var candidates = Enumerable.Range(1, 8)
            .Select(index => new ScoredProduct(
                new Product { Id = Guid.NewGuid(), Sku = $"SKU-{index}", Name = $"Product {index}", SalePrice = 1000m, SaleUnit = "Pcs" },
                new float[] { (float)index },
                (double)index / 10))
            .ToList();

        var result = ProductCandidateAggregator.Rank(candidates, topN: 5);

        Assert.Equal(5, result.Count);
    }
}
