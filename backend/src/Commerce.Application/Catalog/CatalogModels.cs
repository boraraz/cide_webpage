using Commerce.Domain.Catalog;

namespace Commerce.Application.Catalog;

public enum ProductSort
{
    Newest,
    PriceAscending,
    PriceDescending,
}

public sealed record PublicProductQuery(
    int Page,
    int PageSize,
    string? CategorySlug,
    string? CollectionSlug,
    string? Color,
    string? Size,
    decimal? MinPrice,
    decimal? MaxPrice,
    bool AvailableOnly,
    string? Search,
    ProductSort Sort);

public sealed record CategoryResponse(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    Guid? ParentCategoryId,
    int DisplayOrder,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CategoryNode(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    int DisplayOrder,
    bool IsActive,
    IReadOnlyList<CategoryNode> Children);

public sealed record CollectionResponse(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    int DisplayOrder,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record NamedSlug(Guid Id, string Name, string Slug);

public sealed record OptionValueResponse(Guid Id, string Value, string? Code, int Position);

public sealed record OptionResponse(Guid Id, string Name, int Position, IReadOnlyList<OptionValueResponse> Values);

public sealed record SelectedOptionResponse(string Name, string Value, string? Code);

public sealed record VariantResponse(
    Guid Id,
    string Sku,
    string? Barcode,
    bool IsActive,
    string Price,
    string? CompareAtPrice,
    string Currency,
    int OnHand,
    int Reserved,
    int Available,
    IReadOnlyList<SelectedOptionResponse> Options);

public sealed record PublicVariantResponse(
    Guid Id,
    string Sku,
    bool IsActive,
    string Price,
    string? CompareAtPrice,
    string Currency,
    bool Available,
    IReadOnlyList<SelectedOptionResponse> Options);

public sealed record MediaResponse(
    Guid Id,
    Guid? VariantId,
    string AltText,
    int DisplayOrder,
    bool IsPrimary,
    string ContentType,
    long FileSize,
    string Path);

public sealed record ProductResponse(
    Guid Id,
    string Name,
    string Slug,
    string ShortDescription,
    string Description,
    string? Brand,
    bool IsFeatured,
    string Status,
    DateTimeOffset? PublishedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<NamedSlug> Categories,
    IReadOnlyList<NamedSlug> Collections,
    IReadOnlyList<OptionResponse> Options,
    IReadOnlyList<VariantResponse> Variants,
    IReadOnlyList<MediaResponse> Media);

public sealed record PublicProductCard(
    Guid Id,
    string Name,
    string Slug,
    string ShortDescription,
    string? Brand,
    bool IsFeatured,
    string FromPrice,
    string Currency,
    bool Available,
    Guid? PrimaryMediaId);

public sealed record ProductPage(IReadOnlyList<PublicProductCard> Items, int Page, int PageSize, int Total);

public sealed record PublicProductPageResult(ProductPage? Page, bool CategoryMissing, bool CollectionMissing);

public sealed record PublicProductDetail(
    Guid Id,
    string Name,
    string Slug,
    string ShortDescription,
    string Description,
    string? Brand,
    bool IsFeatured,
    DateTimeOffset? PublishedAt,
    string FromPrice,
    string Currency,
    bool Available,
    IReadOnlyList<NamedSlug> Categories,
    IReadOnlyList<NamedSlug> Collections,
    IReadOnlyList<OptionResponse> Options,
    IReadOnlyList<PublicVariantResponse> Variants,
    IReadOnlyList<MediaResponse> Media);

public sealed record AdminProductCard(
    Guid Id,
    string Name,
    string Slug,
    string Status,
    bool IsFeatured,
    int VariantCount,
    DateTimeOffset UpdatedAt);

public sealed record AdminProductPage(IReadOnlyList<AdminProductCard> Items, int Page, int PageSize, int Total);

public sealed record InventoryRow(
    Guid VariantId,
    string Sku,
    Guid ProductId,
    string ProductName,
    int OnHand,
    int Reserved,
    int Available);

public sealed record InventoryPage(IReadOnlyList<InventoryRow> Items, int Page, int PageSize, int Total);

public sealed record MovementResponse(
    Guid Id,
    Guid VariantId,
    string MovementType,
    int QuantityDelta,
    int OnHandAfter,
    int ReservedAfter,
    string? Note,
    Guid? ActorUserId,
    string IdempotencyKey,
    DateTimeOffset CreatedAt);

public sealed record StoredObject(byte[] Content, string ContentType);

public sealed record StockAdjustment(
    Guid VariantId,
    Guid ActorUserId,
    string IdempotencyKey,
    InventoryMovementType MovementType,
    int Quantity,
    string? Note);

public abstract record StockAdjustmentOutcome
{
    public sealed record Applied(InventoryMovement Movement) : StockAdjustmentOutcome;

    public sealed record Missing : StockAdjustmentOutcome;

    public sealed record Insufficient : StockAdjustmentOutcome;

    public sealed record IdempotencyConflict(InventoryMovement Movement) : StockAdjustmentOutcome;
}
