namespace Commerce.Application.Catalog;

public interface ICatalogReader
{
    Task<IReadOnlyList<CategoryNode>> CategoryTreeAsync(bool activeOnly, CancellationToken cancellationToken);

    Task<CategoryNode?> CategoryBySlugAsync(string slug, bool activeOnly, CancellationToken cancellationToken);

    Task<IReadOnlyList<CollectionResponse>> CollectionsAsync(bool activeOnly, CancellationToken cancellationToken);

    Task<CollectionResponse?> CollectionBySlugAsync(string slug, bool activeOnly, CancellationToken cancellationToken);

    Task<PublicProductPageResult> PublicProductsAsync(PublicProductQuery query, CancellationToken cancellationToken);

    Task<PublicCatalogFilterResult> PublicFiltersAsync(PublicProductQuery query, CancellationToken cancellationToken);

    Task<PublicProductDetail?> PublicProductAsync(string slug, CancellationToken cancellationToken);

    Task<IReadOnlyList<PublicProductCard>> RelatedProductsAsync(string slug, int limit, CancellationToken cancellationToken);

    Task<PublicSitemap> PublicSitemapAsync(CancellationToken cancellationToken);

    Task<MediaResponse?> PublicMediaAsync(Guid id, CancellationToken cancellationToken);

    Task<DashboardSummary> DashboardAsync(CancellationToken cancellationToken);

    Task<AdminProductPage> AdminProductsAsync(AdminProductQuery query, CancellationToken cancellationToken);

    Task<ProductResponse?> AdminProductAsync(Guid id, CancellationToken cancellationToken);

    Task<InventoryPage> InventoryAsync(AdminInventoryQuery query, CancellationToken cancellationToken);

    Task<IReadOnlyList<MovementResponse>?> MovementsAsync(Guid variantId, CancellationToken cancellationToken);

    Task<MovementPage> MovementHistoryAsync(MovementHistoryQuery query, CancellationToken cancellationToken);

    Task<MediaResponse?> AdminMediaAsync(Guid id, CancellationToken cancellationToken);
}
