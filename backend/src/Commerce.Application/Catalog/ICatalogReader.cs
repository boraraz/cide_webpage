namespace Commerce.Application.Catalog;

public interface ICatalogReader
{
    Task<IReadOnlyList<CategoryNode>> CategoryTreeAsync(bool activeOnly, CancellationToken cancellationToken);

    Task<CategoryNode?> CategoryBySlugAsync(string slug, bool activeOnly, CancellationToken cancellationToken);

    Task<IReadOnlyList<CollectionResponse>> CollectionsAsync(bool activeOnly, CancellationToken cancellationToken);

    Task<CollectionResponse?> CollectionBySlugAsync(string slug, bool activeOnly, CancellationToken cancellationToken);

    Task<PublicProductPageResult> PublicProductsAsync(PublicProductQuery query, CancellationToken cancellationToken);

    Task<PublicProductDetail?> PublicProductAsync(string slug, CancellationToken cancellationToken);

    Task<MediaResponse?> PublicMediaAsync(Guid id, CancellationToken cancellationToken);

    Task<AdminProductPage> AdminProductsAsync(int page, int pageSize, string? status, CancellationToken cancellationToken);

    Task<ProductResponse?> AdminProductAsync(Guid id, CancellationToken cancellationToken);

    Task<InventoryPage> InventoryAsync(int page, int pageSize, string? sku, CancellationToken cancellationToken);

    Task<IReadOnlyList<MovementResponse>?> MovementsAsync(Guid variantId, CancellationToken cancellationToken);

    Task<MediaResponse?> AdminMediaAsync(Guid id, CancellationToken cancellationToken);
}
