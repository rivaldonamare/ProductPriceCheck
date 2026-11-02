using ElectricalStoreAI.Application.Images;
using ElectricalStoreAI.Application.Recognition;
using ElectricalStoreAI.Domain.Products;
using AppException = ElectricalStoreAI.Application.Common.ApplicationException;

namespace ElectricalStoreAI.Application.Products;

public sealed class ProductImageService : IProductImageService
{
    private readonly IProductRepository _repository;
    private readonly IImageStorageService _storage;
    private readonly IImageEmbeddingService _embeddingService;

    public ProductImageService(IProductRepository repository, IImageStorageService storage, IImageEmbeddingService embeddingService)
    {
        _repository = repository;
        _storage = storage;
        _embeddingService = embeddingService;
    }

    public async Task<IReadOnlyList<ReferenceImageDto>> GetImagesAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var images = await _repository.GetReferenceImagesAsync(productId, cancellationToken);
        var indexed = await _repository.GetIndexedImageIdsAsync(productId, _embeddingService.ModelKey, cancellationToken);

        return images.Select(image => ToDto(image, indexed.Contains(image.Id))).ToList();
    }

    public async Task<AddReferenceImageResult> AddAsync(Guid productId, Stream content, CancellationToken cancellationToken = default)
    {
        _ = await _repository.GetByIdAsync(productId, cancellationToken)
            ?? throw new AppException("The product no longer exists.");

        var stored = await _storage.SaveAsync(content, cancellationToken);
        try
        {
            var image = new ProductReferenceImage
            {
                Id = Guid.NewGuid(),
                ProductId = productId,
                StorageKey = stored.StorageKey,
                ContentType = stored.ContentType,
                CreatedAtUtc = DateTime.UtcNow,
            };

            ProductImageEmbedding? embedding = null;
            string? warning = null;
            try
            {
                var result = await _embeddingService.GenerateEmbeddingAsync(stored.Content, cancellationToken);
                embedding = NewEmbedding(image.Id, result.Vector);
            }
            catch (RecognitionUnavailableException ex)
            {
                // Keep the photo but leave it un-indexed; "Rebuild embeddings" indexes it once the model works.
                warning = "Photo saved but NOT indexed for scanning: " + ex.Message;
            }

            await _repository.AddReferenceImageAsync(image, embedding, cancellationToken);

            return new AddReferenceImageResult(ToDto(image, embedding is not null), warning);
        }
        catch
        {
            await _storage.DeleteAsync(stored.StorageKey, CancellationToken.None);
            throw;
        }
    }

    public async Task DeleteAsync(Guid imageId, CancellationToken cancellationToken = default)
    {
        var image = await _repository.GetReferenceImageAsync(imageId, cancellationToken);
        if (image is null)
        {
            return;
        }

        // Database first: a leftover file is harmless, a row pointing to a missing file is not.
        await _repository.DeleteReferenceImageAsync(imageId, cancellationToken);
        await _storage.DeleteAsync(image.StorageKey, CancellationToken.None);
    }

    public async Task<ImageContent?> OpenAsync(Guid imageId, CancellationToken cancellationToken = default)
    {
        var image = await _repository.GetReferenceImageAsync(imageId, cancellationToken);
        if (image is null)
        {
            return null;
        }

        var bytes = await _storage.ReadAsync(image.StorageKey, cancellationToken);
        return bytes is null ? null : new ImageContent(bytes, image.ContentType);
    }

    public async Task<RebuildEmbeddingsSummary> RebuildEmbeddingsAsync(IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        var images = await _repository.GetAllReferenceImagesAsync(cancellationToken);
        var indexed = 0;
        var failed = 0;

        foreach (var image in images)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var bytes = await _storage.ReadAsync(image.StorageKey, cancellationToken);
            if (bytes is null)
            {
                failed++;
            }
            else
            {
                try
                {
                    var result = await _embeddingService.GenerateEmbeddingAsync(bytes, cancellationToken);
                    await _repository.ReplaceEmbeddingAsync(NewEmbedding(image.Id, result.Vector), cancellationToken);
                    indexed++;
                }
                catch (AppException)
                {
                    // Undecodable image: count it and continue. A missing model aborts the whole run.
                    failed++;
                }
            }

            progress?.Report(indexed + failed);
        }

        return new RebuildEmbeddingsSummary(images.Count, indexed, failed);
    }

    private ProductImageEmbedding NewEmbedding(Guid imageId, float[] vector) => new()
    {
        Id = Guid.NewGuid(),
        ProductReferenceImageId = imageId,
        ModelKey = _embeddingService.ModelKey,
        Vector = vector,
        CreatedAtUtc = DateTime.UtcNow,
    };

    private static ReferenceImageDto ToDto(ProductReferenceImage image, bool isIndexed)
        => new(image.Id, image.ProductId, DateTime.SpecifyKind(image.CreatedAtUtc, DateTimeKind.Utc), isIndexed);
}
