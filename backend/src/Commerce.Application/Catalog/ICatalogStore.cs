using Commerce.Domain.Catalog;

namespace Commerce.Application.Catalog;

public interface ICatalogTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
}

public interface ICatalogStore
{
    Task SaveChangesAsync(CancellationToken cancellationToken);

    Task<CatalogResult<int>> SaveProductGraphAsync(Product product, int? expectedVersion, DateTimeOffset now, CancellationToken cancellationToken);

    Task<ICatalogTransaction> BeginTransactionAsync(CancellationToken cancellationToken);

    void Abandon();

    void Add(object entity);

    void Remove(object entity);

    Task<Category?> FindCategoryAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> CategoryHasChildrenAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> CategoryHasProductsAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> ParentCreatesCycleAsync(Guid categoryId, Guid parentId, CancellationToken cancellationToken);

    Task<Collection?> FindCollectionAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> CollectionHasProductsAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> SlugTakenAsync(SlugOwner owner, string slug, Guid? exceptOwnerId, CancellationToken cancellationToken);

    void RetireSlug(SlugOwner owner, Guid ownerId, string currentSlug, string nextSlug, DateTimeOffset now);

    Task<Product?> FindProductGraphAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Category>> FindCategoriesAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    Task<IReadOnlyList<Collection>> FindCollectionsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    void ReplaceCategories(Product product, IReadOnlyList<Category> categories);

    void ReplaceCollections(Product product, IReadOnlyList<Collection> collections);

    Task<ProductOption?> FindOptionAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> OptionIsUsedAsync(Guid optionId, CancellationToken cancellationToken);

    Task<ProductOptionValue?> FindOptionValueAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> OptionValueIsUsedAsync(Guid optionValueId, CancellationToken cancellationToken);

    Task<ProductVariant?> FindVariantGraphAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> SkuTakenAsync(string sku, Guid? exceptVariantId, CancellationToken cancellationToken);

    Task<bool> BarcodeTakenAsync(string barcode, Guid? exceptVariantId, CancellationToken cancellationToken);

    Task<bool> VariantHasMovementsAsync(Guid variantId, CancellationToken cancellationToken);

    Task<StockAdjustmentOutcome> AdjustStockAsync(StockAdjustment adjustment, CancellationToken cancellationToken);

    Task<ProductMedia?> FindMediaAsync(Guid id, CancellationToken cancellationToken);

    Task ClearPrimaryAsync(Guid productId, Guid exceptMediaId, CancellationToken cancellationToken);
}
