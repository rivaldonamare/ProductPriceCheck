using ElectricalStoreAI.Application.Products;
using ElectricalStoreAI.Application.Recognition;
using ElectricalStoreAI.Domain.Common;
using ElectricalStoreAI.Domain.Products;
using ElectricalStoreAI.Infrastructure.ImageStorage;
using ElectricalStoreAI.Infrastructure.Persistence;
using ElectricalStoreAI.Infrastructure.Recognition;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AppException = ElectricalStoreAI.Application.Common.ApplicationException;

namespace ElectricalStoreAI.IntegrationTests.Persistence;

public sealed class ElectricalStoreAIIntegrationTests : IAsyncLifetime
{
    private static readonly byte[] PngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly string _imageRoot = Path.Combine(Path.GetTempPath(), "ElectricalStoreAITests", Guid.NewGuid().ToString("N"));

    private TestDbContextFactory _factory = null!;
    private AppDbContext _context = null!;
    private ProductRepository _repository = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _factory = new TestDbContextFactory(_connection);
        _context = _factory.CreateDbContext();
        await _context.Database.MigrateAsync();
        _repository = new ProductRepository(_factory);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
        if (Directory.Exists(_imageRoot))
        {
            Directory.Delete(_imageRoot, recursive: true);
        }
    }

    [Fact]
    public async Task SaveProduct_WithDuplicateSku_ShouldFailWithUniqueConstraint()
    {
        var now = DateTime.UtcNow;
        _context.Products.Add(CreateProduct("SKU-DUP", "Product 1", now));
        await _context.SaveChangesAsync();

        _context.Products.Add(CreateProduct("SKU-DUP", "Product 2", now));
        await Assert.ThrowsAsync<DbUpdateException>(() => _context.SaveChangesAsync());
    }

    [Fact]
    public void SaveProduct_WithNegativePrice_ShouldBeRejectedByDomain()
    {
        Assert.Throws<DomainException>(() => Product.Create("SKU-NEG", "Product", null, null, null, -1m, "Pcs"));
    }

    [Fact]
    public async Task Search_ShouldBeCaseInsensitiveAndMatchPartialText()
    {
        await _repository.AddAsync(Product.Create("LED-PHL-10W", "Philips LED Bulb 10W", "Philips", "Lighting", null, 25000m, "Pcs"));
        await _repository.AddAsync(Product.Create("MCB-16A", "MCB 1P 16A", "Schneider", "Protection", null, 60000m, "Pcs"));

        Assert.Single(await _repository.SearchAsync("led bulb", activeOnly: false));
        Assert.Single(await _repository.SearchAsync("phl", activeOnly: false));
        Assert.Single(await _repository.SearchAsync("SCHNEIDER", activeOnly: false));
        Assert.Single(await _repository.SearchAsync("protection", activeOnly: false));
        Assert.Equal(2, (await _repository.SearchAsync(null, activeOnly: false)).Count);
    }

    [Fact]
    public async Task Search_WithActiveOnly_ShouldExcludeInactiveProducts()
    {
        await _repository.AddAsync(Product.Create("SKU-1", "Socket white", null, null, null, 15000m, "Pcs", isActive: false));

        Assert.Empty(await _repository.SearchAsync("socket", activeOnly: true));
        Assert.Single(await _repository.SearchAsync("socket", activeOnly: false));
    }

    [Fact]
    public async Task ExistsBySku_ShouldBeCaseInsensitiveAndHonourExclusion()
    {
        var product = Product.Create("SKU-ABC", "Tape", null, null, null, 5000m, "Roll");
        await _repository.AddAsync(product);

        Assert.True(await _repository.ExistsBySkuAsync("sku-abc"));
        Assert.False(await _repository.ExistsBySkuAsync("sku-abc", product.Id));
    }

    [Fact]
    public async Task UpdateSameProductTwice_ShouldSucceed()
    {
        // Regression: a circuit-scoped DbContext threw "another instance with the same key is already being tracked".
        var service = new ProductService(_repository);
        var created = await service.CreateAsync(new ProductCreateDto("SKU-1", "Cable", null, null, null, 8000m, "Meter"));

        await service.UpdateAsync(new ProductUpdateDto(created.Id, "SKU-1", "Cable NYA", null, null, null, 8500m, "Meter", true));
        var updated = await service.UpdateAsync(new ProductUpdateDto(created.Id, "SKU-1", "Cable NYA 1.5", null, null, null, 9000m, "Meter", true));

        Assert.Equal(9000m, updated.SalePrice);
        Assert.Equal(9000m, (await _repository.GetByIdAsync(created.Id))!.SalePrice);
    }

    [Fact]
    public async Task ActiveEmbeddingRefs_WithInactiveProduct_ShouldReturnNoEntries()
    {
        var product = Product.Create("SKU-LED-10", "LED Bulb 10W", null, null, null, 10000m, "Pcs", isActive: false);
        await SeedImageWithEmbeddingAsync(product, FakeEmbeddingService.Key);

        Assert.Empty(await _repository.GetActiveEmbeddingRefsAsync(FakeEmbeddingService.Key));
    }

    [Fact]
    public async Task ActiveEmbeddingRefs_WithDifferentModelKey_ShouldReturnNoEntries()
    {
        var product = Product.Create("SKU-LED-10", "LED Bulb 10W", null, null, null, 10000m, "Pcs");
        await SeedImageWithEmbeddingAsync(product, "a-different-model");

        Assert.Empty(await _repository.GetActiveEmbeddingRefsAsync(FakeEmbeddingService.Key));
    }

    [Fact]
    public async Task Recognition_ShouldReturnDistinctActiveProductsWithDatabasePrices()
    {
        var bulb = Product.Create("SKU-BULB", "Bulb", null, null, null, 10000m, "Pcs");
        var mcb = Product.Create("SKU-MCB", "MCB", null, null, null, 50000m, "Pcs");
        await SeedImageWithEmbeddingAsync(bulb, FakeEmbeddingService.Key, [1f, 0f, 0f]);
        await SeedImageWithEmbeddingAsync(bulb, FakeEmbeddingService.Key, [0.9f, 0.1f, 0f]);
        await SeedImageWithEmbeddingAsync(mcb, FakeEmbeddingService.Key, [0f, 1f, 0f]);

        var recognition = new ProductRecognitionService(_repository, new FakeEmbeddingService(), new EmbeddingVectorCache(), Options.Create(new RecognitionOptions()));
        var result = await recognition.FindCandidatesAsync([1f, 0f, 0f]);

        Assert.Equal(3, result.IndexedImageCount);
        Assert.Equal(["SKU-BULB", "SKU-MCB"], result.Candidates.Select(candidate => candidate.Sku));
        Assert.Equal(10000m, result.Candidates[0].SalePrice);
        Assert.True(result.IsConfident);
    }

    [Fact]
    public async Task DeleteReferenceImage_ShouldDeleteEmbeddingAndFile()
    {
        var product = Product.Create("SKU-LED-10", "LED Bulb 10W", null, null, null, 10000m, "Pcs");
        await _repository.AddAsync(product);
        var service = new ProductImageService(_repository, CreateStorage(), new FakeEmbeddingService());

        var added = await service.AddAsync(product.Id, new MemoryStream(PngHeader));
        Assert.True(added.Image.IsIndexed);
        Assert.Single(Directory.GetFiles(_imageRoot));

        await service.DeleteAsync(added.Image.Id);

        Assert.False(await _context.ProductImageEmbeddings.AnyAsync(e => e.ProductReferenceImageId == added.Image.Id));
        Assert.False(await _context.ProductReferenceImages.AnyAsync(i => i.Id == added.Image.Id));
        Assert.Empty(Directory.GetFiles(_imageRoot));
    }

    [Fact]
    public async Task AddReferenceImage_WhenModelUnavailable_ShouldKeepPhotoUnindexedWithWarning()
    {
        var product = Product.Create("SKU-LED-10", "LED Bulb 10W", null, null, null, 10000m, "Pcs");
        await _repository.AddAsync(product);
        var service = new ProductImageService(_repository, CreateStorage(), new FakeEmbeddingService(throwUnavailable: true));

        var added = await service.AddAsync(product.Id, new MemoryStream(PngHeader));

        Assert.False(added.Image.IsIndexed);
        Assert.NotNull(added.Warning);
        Assert.Single(await service.GetImagesAsync(product.Id));
    }

    [Fact]
    public async Task ImageStorageService_WithValidImage_ShouldSaveReadAndDelete()
    {
        var storage = CreateStorage();

        var stored = await storage.SaveAsync(new MemoryStream(PngHeader));

        Assert.Equal("image/png", stored.ContentType);
        Assert.Equal(PngHeader, await storage.ReadAsync(stored.StorageKey));
        await storage.DeleteAsync(stored.StorageKey);
        Assert.Null(await storage.ReadAsync(stored.StorageKey));
    }

    [Fact]
    public async Task ImageStorageService_WithNonImageContent_ShouldThrow()
    {
        var storage = CreateStorage();

        await Assert.ThrowsAsync<AppException>(() => storage.SaveAsync(new MemoryStream("not an image at all"u8.ToArray())));
    }

    [Fact]
    public async Task ImageStorageService_WithOversizedImage_ShouldThrow()
    {
        var storage = CreateStorage(maxBytes: 1024);
        var bytes = new byte[2048];
        PngHeader.CopyTo(bytes, 0);

        await Assert.ThrowsAsync<AppException>(() => storage.SaveAsync(new MemoryStream(bytes)));
    }

    [Theory]
    [InlineData("../secret.jpg")]
    [InlineData("..\\..\\Windows\\win.ini")]
    [InlineData("C:\\Windows\\win.ini")]
    [InlineData("/etc/passwd")]
    public async Task ImageStorageService_WithUnsafeKey_ShouldRejectWithoutTouchingFilesystem(string key)
    {
        var storage = CreateStorage();

        await Assert.ThrowsAsync<AppException>(() => storage.ReadAsync(key));
        await Assert.ThrowsAsync<AppException>(() => storage.DeleteAsync(key));
    }

    private ImageStorageService CreateStorage(long maxBytes = 10 * 1024 * 1024)
        => new(Options.Create(new ImageStorageOptions { RootDirectory = _imageRoot, MaxFileSizeBytes = maxBytes }));

    private async Task SeedImageWithEmbeddingAsync(Product product, string modelKey, float[]? vector = null)
    {
        if (!await _context.Products.AnyAsync(p => p.Id == product.Id))
        {
            _context.Products.Add(product);
        }

        var image = new ProductReferenceImage
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            StorageKey = $"{Guid.NewGuid():N}.jpg",
            ContentType = "image/jpeg",
            CreatedAtUtc = DateTime.UtcNow,
        };
        _context.ProductReferenceImages.Add(image);
        _context.ProductImageEmbeddings.Add(new ProductImageEmbedding
        {
            Id = Guid.NewGuid(),
            ProductReferenceImageId = image.Id,
            ModelKey = modelKey,
            Vector = vector ?? [1f, 0f, 0f],
            CreatedAtUtc = DateTime.UtcNow,
        });
        await _context.SaveChangesAsync();
    }

    private static Product CreateProduct(string sku, string name, DateTime now) => new()
    {
        Id = Guid.NewGuid(),
        Sku = sku,
        Name = name,
        SalePrice = 1000m,
        SaleUnit = "Pcs",
        IsActive = true,
        CreatedAtUtc = now,
        UpdatedAtUtc = now,
    };

    private sealed class TestDbContextFactory(SqliteConnection connection) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext()
            => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
    }

    private sealed class FakeEmbeddingService(bool throwUnavailable = false) : IImageEmbeddingService
    {
        public const string Key = "fake-model-v1";

        public string ModelKey => Key;

        public EmbeddingModelStatus Status => throwUnavailable ? EmbeddingModelStatus.Missing : EmbeddingModelStatus.Ready;

        public Task WarmUpAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<ImageEmbeddingResult> GenerateEmbeddingAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
        {
            if (throwUnavailable)
            {
                throw new RecognitionUnavailableException("Model missing.");
            }

            return Task.FromResult(new ImageEmbeddingResult([1f, 0f, 0f], TimeSpan.Zero, TimeSpan.Zero));
        }
    }
}
