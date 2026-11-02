namespace ElectricalStoreAI.Application.Products;

public interface IProductService
{
    Task<ProductDto> CreateAsync(ProductCreateDto request, CancellationToken cancellationToken = default);

    Task<ProductDto> UpdateAsync(ProductUpdateDto request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductDto>> SearchAsync(string? query, bool activeOnly = false, CancellationToken cancellationToken = default);

    Task<ProductDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);
}
