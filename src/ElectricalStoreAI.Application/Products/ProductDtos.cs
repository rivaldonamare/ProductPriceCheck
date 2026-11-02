using ElectricalStoreAI.Application.Common;

namespace ElectricalStoreAI.Application.Products;

public sealed record ProductDto(
    Guid Id,
    string Sku,
    string Name,
    string? Brand,
    string? Category,
    string? Description,
    decimal SalePrice,
    string SaleUnit,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc)
{
    public string FormattedSalePrice => RupiahFormatter.Format(SalePrice);
}

public sealed record ProductCreateDto(
    string Sku,
    string Name,
    string? Brand,
    string? Category,
    string? Description,
    decimal SalePrice,
    string SaleUnit);

public sealed record ProductUpdateDto(
    Guid Id,
    string Sku,
    string Name,
    string? Brand,
    string? Category,
    string? Description,
    decimal SalePrice,
    string SaleUnit,
    bool IsActive);

public sealed record ReferenceImageDto(Guid Id, Guid ProductId, DateTime CreatedAtUtc, bool IsIndexed);

/// <param name="Warning">Set when the photo was stored but could not be indexed for recognition.</param>
public sealed record AddReferenceImageResult(ReferenceImageDto Image, string? Warning);

public sealed record ImageContent(byte[] Content, string ContentType);

public sealed record RebuildEmbeddingsSummary(int Total, int Indexed, int Failed);
