using ElectricalStoreAI.Domain.Similarity;

namespace ElectricalStoreAI.UnitTests.Domain.Similarity;

public sealed class EmbeddingSimilarityTests
{
    [Fact]
    public void CosineSimilarity_WithIdenticalVectors_ShouldReturnOne()
    {
        var vector = new float[] { 0.5f, 0.5f, 0.5f };

        var similarity = EmbeddingSimilarity.CosineSimilarity(vector, vector);

        Assert.Equal(1.0, similarity, 5);
    }

    [Fact]
    public void CosineSimilarity_WithOrthogonalVectors_ShouldReturnZero()
    {
        var similarity = EmbeddingSimilarity.CosineSimilarity(
            new float[] { 1f, 0f, 0f },
            new float[] { 0f, 1f, 0f });

        Assert.Equal(0.0, similarity, 5);
    }

    [Fact]
    public void CosineSimilarity_WithEmptyVectors_ShouldReturnZero()
    {
        Assert.Equal(0.0, EmbeddingSimilarity.CosineSimilarity(Array.Empty<float>(), Array.Empty<float>()));
    }

    [Fact]
    public void CosineSimilarity_WithMismatchedVectorLengths_ShouldReturnZero()
    {
        Assert.Equal(0.0, EmbeddingSimilarity.CosineSimilarity(new float[] { 1f, 0f }, new float[] { 1f, 0f, 0f }));
    }
}
