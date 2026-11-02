using ElectricalStoreAI.Application.Products;
using ElectricalStoreAI.Application.Recognition;
using ElectricalStoreAI.Domain.Products;
using Microsoft.EntityFrameworkCore;

namespace ElectricalStoreAI.Infrastructure.Persistence;

/// <summary>
/// Uses a short-lived DbContext per operation. Blazor Server keeps scoped services alive for the whole
/// circuit, so a scoped DbContext would accumulate tracked entities and fail on concurrent/repeated use.
/// </summary>
public sealed class ProductRepository : IProductRepository
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public ProductRepository(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Products.AsNoTracking().FirstOrDefaultAsync(product => product.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Product>> SearchAsync(string? query, bool activeOnly, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var products = db.Products.AsNoTracking();

        if (activeOnly)
        {
            products = products.Where(product => product.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            // ToLower() + Contains translates to lower()/instr() on SQLite and LOWER/LIKE elsewhere:
            // consistent case-insensitive matching without LIKE wildcard escaping issues.
            var term = query.Trim().ToLowerInvariant();
            products = products.Where(product =>
                product.Name.ToLower().Contains(term)
                || product.Sku.ToLower().Contains(term)
                || (product.Brand != null && product.Brand.ToLower().Contains(term))
                || (product.Category != null && product.Category.ToLower().Contains(term)));
        }

        return await products.OrderBy(product => product.Name).ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsBySkuAsync(string sku, Guid? excludeProductId = null, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var normalized = sku.Trim().ToLowerInvariant();
        return await db.Products.AnyAsync(
            product => product.Sku.ToLower() == normalized && (excludeProductId == null || product.Id != excludeProductId),
            cancellationToken);
    }

    public async Task AddAsync(Product product, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Product product, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        db.Products.Update(product);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ProductReferenceImage?> GetReferenceImageAsync(Guid imageId, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.ProductReferenceImages.AsNoTracking().FirstOrDefaultAsync(image => image.Id == imageId, cancellationToken);
    }

    public async Task<IReadOnlyList<ProductReferenceImage>> GetReferenceImagesAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.ProductReferenceImages
            .AsNoTracking()
            .Where(image => image.ProductId == productId)
            .OrderByDescending(image => image.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProductReferenceImage>> GetAllReferenceImagesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.ProductReferenceImages.AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlySet<Guid>> GetIndexedImageIdsAsync(Guid productId, string modelKey, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var ids = await (
                from embedding in db.ProductImageEmbeddings
                join image in db.ProductReferenceImages on embedding.ProductReferenceImageId equals image.Id
                where image.ProductId == productId && embedding.ModelKey == modelKey
                select image.Id)
            .Distinct()
            .ToListAsync(cancellationToken);

        return ids.ToHashSet();
    }

    public async Task AddReferenceImageAsync(ProductReferenceImage image, ProductImageEmbedding? embedding, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        db.ProductReferenceImages.Add(image);
        if (embedding is not null)
        {
            db.ProductImageEmbeddings.Add(embedding);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ReplaceEmbeddingAsync(ProductImageEmbedding embedding, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        await db.ProductImageEmbeddings
            .Where(existing => existing.ProductReferenceImageId == embedding.ProductReferenceImageId)
            .ExecuteDeleteAsync(cancellationToken);
        db.ProductImageEmbeddings.Add(embedding);
        await db.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task DeleteReferenceImageAsync(Guid productReferenceImageId, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Explicit delete so embeddings go away even if SQLite foreign-key enforcement is off.
        await db.ProductImageEmbeddings
            .Where(embedding => embedding.ProductReferenceImageId == productReferenceImageId)
            .ExecuteDeleteAsync(cancellationToken);
        await db.ProductReferenceImages
            .Where(image => image.Id == productReferenceImageId)
            .ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ActiveEmbeddingRef>> GetActiveEmbeddingRefsAsync(string modelKey, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await (
                from embedding in db.ProductImageEmbeddings.AsNoTracking()
                join image in db.ProductReferenceImages on embedding.ProductReferenceImageId equals image.Id
                join product in db.Products.AsNoTracking() on image.ProductId equals product.Id
                where embedding.ModelKey == modelKey && product.IsActive
                select new { embedding.Id, product })
            .ToListAsync(cancellationToken);

        return rows.Select(row => new ActiveEmbeddingRef(row.Id, row.product)).ToList();
    }

    public async Task<IReadOnlyDictionary<Guid, float[]>> GetEmbeddingVectorsAsync(IReadOnlyCollection<Guid> embeddingIds, CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<Guid, float[]>(embeddingIds.Count);
        if (embeddingIds.Count == 0)
        {
            return result;
        }

        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        foreach (var chunk in embeddingIds.Chunk(500))
        {
            var rows = await db.ProductImageEmbeddings
                .AsNoTracking()
                .Where(embedding => chunk.Contains(embedding.Id))
                .Select(embedding => new { embedding.Id, embedding.Vector })
                .ToListAsync(cancellationToken);

            foreach (var row in rows)
            {
                result[row.Id] = row.Vector;
            }
        }

        return result;
    }
}
