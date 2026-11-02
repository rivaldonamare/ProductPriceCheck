namespace ElectricalStoreAI.Application.Recognition;

/// <summary>
/// Uncalibrated heuristics used only to decide whether to warn "Product not confidently identified".
/// Tune them with a validation dataset before trusting them.
/// </summary>
public sealed class RecognitionOptions
{
    public int TopN { get; set; } = 5;

    public double MinTopScore { get; set; } = 0.80;

    public double MinMarginToSecond { get; set; } = 0.02;
}

public static class RecognitionConfidence
{
    public static bool IsConfident(IReadOnlyList<RecognitionCandidate> ranked, RecognitionOptions options)
    {
        if (ranked.Count == 0 || ranked[0].SimilarityScore < options.MinTopScore)
        {
            return false;
        }

        return ranked.Count == 1 || ranked[0].SimilarityScore - ranked[1].SimilarityScore >= options.MinMarginToSecond;
    }
}
