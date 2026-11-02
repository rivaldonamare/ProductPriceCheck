namespace ElectricalStoreAI.Application.Recognition;

public enum EmbeddingModelStatus
{
    NotLoaded,
    Loading,
    Ready,
    Missing,
    Failed,
}

public sealed record ImageEmbeddingResult(float[] Vector, TimeSpan PreprocessingTime, TimeSpan InferenceTime);

/// <summary>
/// Generates local image embeddings using the configured local inference model.
/// </summary>
public interface IImageEmbeddingService
{
    /// <summary>
    /// Gets the key identifying the embedding model used for generated vectors.
    /// Vectors from a different model key must never be compared.
    /// </summary>
    string ModelKey { get; }

    EmbeddingModelStatus Status { get; }

    /// <summary>Loads the model and runs one dummy inference so the first real scan is fast.</summary>
    Task WarmUpAsync(CancellationToken cancellationToken = default);

    /// <exception cref="RecognitionUnavailableException">The model is missing or invalid.</exception>
    /// <exception cref="Common.ApplicationException">The image could not be decoded.</exception>
    Task<ImageEmbeddingResult> GenerateEmbeddingAsync(byte[] imageBytes, CancellationToken cancellationToken = default);
}
