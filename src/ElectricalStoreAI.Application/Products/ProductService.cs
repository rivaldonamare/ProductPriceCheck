using ElectricalStoreAI.Domain.Products;
using AppException = ElectricalStoreAI.Application.Common.ApplicationException;

namespace ElectricalStoreAI.Application.Products;

public sealed class ProductService : IProductService
{
    private readonly IProductRepository _repository;

    public ProductService(IProductRepository repository)
    {
        _repository = repository;
    }

    public async Task<ProductDto> CreateAsync(ProductCreateDto request, CancellationToken cancellationToken = default)
    {
        // Domain validation first so required-field errors win over the SKU lookup.
        var product = Product.Create(
            request.Sku?.Trim() ?? string.Empty,
            request.Name?.Trim() ?? string.Empty,
            Normalize(request.Brand),
            Normalize(request.Category),
            Normalize(request.Description),
            request.SalePrice,
            request.SaleUnit?.Trim() ?? string.Empty,
            isActive: true);

        if (await _repository.ExistsBySkuAsync(product.Sku, null, cancellationToken))
        {
            throw new AppException($"A product with SKU '{product.Sku}' already exists.");
        }

        await _repository.AddAsync(product, cancellationToken);

        return ToDto(product);
    }

    public async Task<ProductDto> UpdateAsync(ProductUpdateDto request, CancellationToken cancellationToken = default)
    {
        var product = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new AppException("The requested product could not be found.");

        product.Sku = request.Sku?.Trim() ?? string.Empty;
        product.Name = request.Name?.Trim() ?? string.Empty;
        product.Brand = Normalize(request.Brand);
        product.Category = Normalize(request.Category);
        product.Description = Normalize(request.Description);
        product.SalePrice = request.SalePrice;
        product.SaleUnit = request.SaleUnit?.Trim() ?? string.Empty;
        product.IsActive = request.IsActive;
        product.Validate();

        if (await _repository.ExistsBySkuAsync(product.Sku, product.Id, cancellationToken))
        {
            throw new AppException($"Another product already uses SKU '{product.Sku}'.");
        }

        product.UpdatedAtUtc = DateTime.UtcNow;
        await _repository.UpdateAsync(product, cancellationToken);

        return ToDto(product);
    }

    public async Task<IReadOnlyList<ProductDto>> SearchAsync(string? query, bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        var products = await _repository.SearchAsync(query, activeOnly, cancellationToken);

        return products.Select(ToDto).ToList();
    }

    public async Task<ProductDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await _repository.GetByIdAsync(id, cancellationToken);

        return product is null ? null : ToDto(product);
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ProductDto ToDto(Product product) => new(
        product.Id,
        product.Sku,
        product.Name,
        product.Brand,
        product.Category,
        product.Description,
        product.SalePrice,
        product.SaleUnit,
        product.IsActive,
        DateTime.SpecifyKind(product.CreatedAtUtc, DateTimeKind.Utc),
        DateTime.SpecifyKind(product.UpdatedAtUtc, DateTimeKind.Utc));
}
