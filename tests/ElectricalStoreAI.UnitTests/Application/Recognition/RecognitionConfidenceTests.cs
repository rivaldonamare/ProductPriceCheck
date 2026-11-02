using ElectricalStoreAI.Application.Common;
using ElectricalStoreAI.Application.Recognition;

namespace ElectricalStoreAI.UnitTests.Application.Recognition;

public sealed class RecognitionConfidenceTests
{
    private static readonly RecognitionOptions Options = new() { MinTopScore = 0.8, MinMarginToSecond = 0.02 };

    [Fact]
    public void IsConfident_WithNoCandidates_ShouldBeFalse()
        => Assert.False(RecognitionConfidence.IsConfident([], Options));

    [Fact]
    public void IsConfident_WithLowTopScore_ShouldBeFalse()
        => Assert.False(RecognitionConfidence.IsConfident([Candidate(0.7)], Options));

    [Fact]
    public void IsConfident_WithTopTwoTooClose_ShouldBeFalse()
        => Assert.False(RecognitionConfidence.IsConfident([Candidate(0.91), Candidate(0.90)], Options));

    [Fact]
    public void IsConfident_WithHighAndDistinctTopScore_ShouldBeTrue()
        => Assert.True(RecognitionConfidence.IsConfident([Candidate(0.92), Candidate(0.85)], Options));

    [Theory]
    [InlineData(10000, "10.000")]
    [InlineData(1250.5, "1.250,50")]
    public void RupiahFormatter_ShouldUseIndonesianFormat(decimal amount, string expectedDigits)
    {
        var formatted = RupiahFormatter.Format(amount);

        Assert.StartsWith("Rp", formatted);
        Assert.EndsWith(expectedDigits, formatted);
    }

    private static RecognitionCandidate Candidate(double score)
        => new(Guid.NewGuid(), "SKU", "Name", null, 1000m, "Pcs", score);
}
