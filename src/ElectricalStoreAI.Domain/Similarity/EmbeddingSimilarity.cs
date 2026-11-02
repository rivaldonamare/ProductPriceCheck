namespace ElectricalStoreAI.Domain.Similarity;

/// <summary>
/// Computes cosine similarity between embedding vectors.
/// The result is a ranking score in the range [0, 1]; it is NOT a calibrated probability.
/// </summary>
public static class EmbeddingSimilarity
{
    public const double MaxSimilarity = 1.0;

    public static double CosineSimilarity(float[]? left, float[]? right)
    {
        if (left is null || right is null || left.Length == 0 || right.Length == 0)
        {
            return 0;
        }

        if (left.Length != right.Length)
        {
            return 0;
        }

        double dotProduct = 0.0;
        double leftNormSquared = 0.0;
        double rightNormSquared = 0.0;

        for (var i = 0; i < left.Length; i++)
        {
            dotProduct += left[i] * right[i];
            leftNormSquared += left[i] * left[i];
            rightNormSquared += right[i] * right[i];
        }

        var denominator = Math.Sqrt(leftNormSquared) * Math.Sqrt(rightNormSquared);
        if (denominator == 0.0)
        {
            return 0;
        }

        var similarity = dotProduct / denominator;
        return Math.Clamp(similarity, -1.0, MaxSimilarity);
    }
}
