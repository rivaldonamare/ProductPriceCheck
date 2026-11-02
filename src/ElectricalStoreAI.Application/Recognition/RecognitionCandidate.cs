using ElectricalStoreAI.Application.Common;

namespace ElectricalStoreAI.Application.Recognition;

/// <summary>
/// A ranked product candidate for a scan result. The similarity score is a ranking score
/// computed from local embeddings; it is NOT a calibrated probability.
/// </summary>
public sealed record RecognitionCandidate(
    Guid ProductId,
    string Sku,
    string Name,
    string? Brand,
    decimal SalePrice,
    string SaleUnit,
    double SimilarityScore)
{
    public string FormattedSalePrice => RupiahFormatter.Format(SalePrice);
}

/// <param name="IsConfident">
/// False when the top score is low or the top two candidates are too close. Thresholds are uncalibrated
/// heuristics: this flag only controls the warning shown to the cashier; nothing is ever auto-selected.
/// </param>
public sealed record RecognitionResult(
    IReadOnlyList<RecognitionCandidate> Candidates,
    bool IsConfident,
    int IndexedImageCount,
    TimeSpan MatchingTime);

/// <summary>An embedding id of an active product's reference image (vector loaded separately and cached).</summary>
public sealed record ActiveEmbeddingRef(Guid EmbeddingId, ElectricalStoreAI.Domain.Products.Product Product);
