namespace ElectricalStoreAI.Application.Products;

public interface IProductImageService
{
    Task<IReadOnlyList<ReferenceImageDto>> GetImagesAsync(Guid productId, CancellationToken cancellationToken = default);

    Task<AddReferenceImageResult> AddAsync(Guid productId, Stream content, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid imageId, CancellationToken cancellationToken = default);

    Task<ImageContent?> OpenAsync(Guid imageId, CancellationToken cancellationToken = default);

    /// <summary>Re-generates embeddings of every reference image with the current model.</summary>
    Task<RebuildEmbeddingsSummary> RebuildEmbeddingsAsync(IProgress<int>? progress = null, CancellationToken cancellationToken = default);
}
