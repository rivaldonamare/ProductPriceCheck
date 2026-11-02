using ElectricalStoreAI.Application.Products;
using ElectricalStoreAI.Application.Recognition;
using ElectricalStoreAI.Domain.Common;
using ElectricalStoreAI.Domain.Products;
using AppException = ElectricalStoreAI.Application.Common.ApplicationException;

namespace ElectricalStoreAI.UnitTests.Application.Products;

public sealed class ProductServiceTests
{
    private readonly FakeProductRepository _repository = new();
    private readonly ProductService _sut;

    public ProductServiceTests()
    {
        _sut = new ProductService(_repository);
    }

    [Fact]
    public async Task CreateAsync_WithValidRequest_ShouldAddAndReturnProduct()
    {
        var request = new ProductCreateDto("SKU-LED-10", "LED Bulb 10W", "Brand", "Lighting", null, 10000m, "Pcs");

        var result = await _sut.CreateAsync(request);

        Assert.Equal("SKU-LED-10", result.Sku);
        Assert.Equal("LED Bulb 10W", result.Name);
        Assert.Equal(10000m, result.SalePrice);
        Assert.Single(_repository.AddedProducts);
    }

    [Fact]
    public async Task CreateAsync_ShouldTrimTextAndTreatBlankOptionalFieldsAsNull()
    {
        var result = await _sut.CreateAsync(new ProductCreateDto("  SKU-1 ", " Tape ", "  ", null, " ", 5000m, " Roll "));

        Assert.Equal("SKU-1", result.Sku);
        Assert.Equal("Tape", result.Name);
        Assert.Null(result.Brand);
        Assert.Null(result.Description);
        Assert.Equal("Roll", result.SaleUnit);
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateSku_ShouldThrowApplicationException()
    {
        _repository.SkuExistsResult = true;

        var request = new ProductCreateDto("SKU-LED-10", "LED Bulb 10W", null, null, null, 10000m, "Pcs");

        await Assert.ThrowsAsync<AppException>(() => _sut.CreateAsync(request));
        Assert.Empty(_repository.AddedProducts);
    }

    [Fact]
    public async Task CreateAsync_WithNegativePrice_ShouldThrowDomainException()
    {
        var request = new ProductCreateDto("SKU-LED-10", "LED Bulb 10W", null, null, null, -1m, "Pcs");

        await Assert.ThrowsAsync<DomainException>(() => _sut.CreateAsync(request));
    }

    [Fact]
    public async Task CreateAsync_WithMissingName_ShouldThrowDomainException()
    {
        var request = new ProductCreateDto("SKU-LED-10", string.Empty, null, null, null, 10000m, "Pcs");

        await Assert.ThrowsAsync<DomainException>(() => _sut.CreateAsync(request));
    }

    [Fact]
    public async Task UpdateAsync_WithSkuUsedByAnotherProduct_ShouldThrowApplicationException()
    {
        var existing = Product.Create("SKU-A", "A", null, null, null, 1000m, "Pcs");
        _repository.ProductById = existing;
        _repository.SkuExistsResult = true;

        var request = new ProductUpdateDto(existing.Id, "SKU-B", "A", null, null, null, 1000m, "Pcs", true);

        await Assert.ThrowsAsync<AppException>(() => _sut.UpdateAsync(request));
        Assert.Equal(existing.Id, _repository.LastExcludedProductId);
        Assert.Empty(_repository.UpdatedProducts);
    }

    [Fact]
    public async Task UpdateAsync_WithNegativePrice_ShouldThrowDomainException()
    {
        var existing = Product.Create("SKU-A", "A", null, null, null, 1000m, "Pcs");
        _repository.ProductById = existing;

        var request = new ProductUpdateDto(existing.Id, "SKU-A", "A", null, null, null, -5m, "Pcs", true);

        await Assert.ThrowsAsync<DomainException>(() => _sut.UpdateAsync(request));
    }

    private sealed class FakeProductRepository : IProductRepository
    {
        public List<Product> AddedProducts { get; } = [];

        public List<Product> UpdatedProducts { get; } = [];

        public bool SkuExistsResult { get; set; }

        public Product? ProductById { get; set; }

        public Guid? LastExcludedProductId { get; private set; }

        public Task AddAsync(Product product, CancellationToken cancellationToken = default)
        {
            AddedProducts.Add(product);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Product product, CancellationToken cancellationToken = default)
        {
            UpdatedProducts.Add(product);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsBySkuAsync(string sku, Guid? excludeProductId = null, CancellationToken cancellationToken = default)
        {
            LastExcludedProductId = excludeProductId;
            return Task.FromResult(SkuExistsResult);
        }

        public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(ProductById?.Id == id ? ProductById : null);

        public Task<IReadOnlyList<Product>> SearchAsync(string? query, bool activeOnly, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Product>>([]);

        public Task<ProductReferenceImage?> GetReferenceImageAsync(Guid imageId, CancellationToken cancellationToken = default)
            => Task.FromResult<ProductReferenceImage?>(null);

        public Task<IReadOnlyList<ProductReferenceImage>> GetReferenceImagesAsync(Guid productId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProductReferenceImage>>([]);

        public Task<IReadOnlyList<ProductReferenceImage>> GetAllReferenceImagesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProductReferenceImage>>([]);

        public Task<IReadOnlySet<Guid>> GetIndexedImageIdsAsync(Guid productId, string modelKey, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>());

        public Task AddReferenceImageAsync(ProductReferenceImage image, ProductImageEmbedding? embedding, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task ReplaceEmbeddingAsync(ProductImageEmbedding embedding, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task DeleteReferenceImageAsync(Guid productReferenceImageId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<ActiveEmbeddingRef>> GetActiveEmbeddingRefsAsync(string modelKey, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ActiveEmbeddingRef>>([]);

        public Task<IReadOnlyDictionary<Guid, float[]>> GetEmbeddingVectorsAsync(IReadOnlyCollection<Guid> embeddingIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<Guid, float[]>>(new Dictionary<Guid, float[]>());
    }
}
