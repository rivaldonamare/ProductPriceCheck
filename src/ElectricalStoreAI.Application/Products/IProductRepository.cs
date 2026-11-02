using ElectricalStoreAI.Application.Recognition;
using ElectricalStoreAI.Domain.Products;

namespace ElectricalStoreAI.Application.Products;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Case-insensitive search on name, SKU, brand and category.</summary>
    Task<IReadOnlyList<Product>> SearchAsync(string? query, bool activeOnly, CancellationToken cancellationToken = default);

    /// <summary>Case-insensitive SKU existence check, optionally ignoring one product (for updates).</summary>
    Task<bool> ExistsBySkuAsync(string sku, Guid? excludeProductId = null, CancellationToken cancellationToken = default);

    Task AddAsync(Product product, CancellationToken cancellationToken = default);

    Task UpdateAsync(Product product, CancellationToken cancellationToken = default);

    Task<ProductReferenceImage?> GetReferenceImageAsync(Guid imageId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductReferenceImage>> GetReferenceImagesAsync(Guid productId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductReferenceImage>> GetAllReferenceImagesAsync(CancellationToken cancellationToken = default);

    /// <summary>Ids of the product's images that have an embedding for <paramref name="modelKey"/>.</summary>
    Task<IReadOnlySet<Guid>> GetIndexedImageIdsAsync(Guid productId, string modelKey, CancellationToken cancellationToken = default);

    /// <summary>Saves the image and (optionally) its embedding in one transaction.</summary>
    Task AddReferenceImageAsync(ProductReferenceImage image, ProductImageEmbedding? embedding, CancellationToken cancellationToken = default);

    /// <summary>Removes every existing embedding of the image and stores <paramref name="embedding"/>.</summary>
    Task ReplaceEmbeddingAsync(ProductImageEmbedding embedding, CancellationToken cancellationToken = default);

    /// <summary>Deletes the image row and its embeddings.</summary>
    Task DeleteReferenceImageAsync(Guid productReferenceImageId, CancellationToken cancellationToken = default);

    /// <summary>Embeddings of <paramref name="modelKey"/> belonging to active products only.</summary>
    Task<IReadOnlyList<ActiveEmbeddingRef>> GetActiveEmbeddingRefsAsync(string modelKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, float[]>> GetEmbeddingVectorsAsync(IReadOnlyCollection<Guid> embeddingIds, CancellationToken cancellationToken = default);
}
