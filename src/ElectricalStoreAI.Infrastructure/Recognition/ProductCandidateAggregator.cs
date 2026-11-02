using ElectricalStoreAI.Application.Recognition;
using ElectricalStoreAI.Domain.Products;

namespace ElectricalStoreAI.Infrastructure.Recognition;

/// <summary>
/// Aggregates raw (product, embedding, similarity) scores into product-level candidates.
/// A product with multiple matching reference images is scored once (best image wins).
/// </summary>
public static class ProductCandidateAggregator
{
    public const int DefaultTopN = 5;

    public static IReadOnlyList<RecognitionCandidate> Rank(
        IEnumerable<ScoredProduct> scoredCandidates,
        int topN = DefaultTopN)
    {
        var bestPerProduct = scoredCandidates
            .GroupBy(candidate => candidate.Product.Id)
            .Select(group => group.OrderByDescending(candidate => candidate.SimilarityScore).First());

        var ordered = bestPerProduct
            .Select(best => new RecognitionCandidate(
                best.Product.Id,
                best.Product.Sku,
                best.Product.Name,
                best.Product.Brand,
                best.Product.SalePrice,
                best.Product.SaleUnit,
                best.SimilarityScore))
            .OrderByDescending(candidate => candidate.SimilarityScore)
            .Take(topN)
            .ToList();

        return ordered;
    }
}
