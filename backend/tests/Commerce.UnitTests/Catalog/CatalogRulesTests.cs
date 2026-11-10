using Commerce.Application.Catalog;
using Commerce.Domain.Catalog;

namespace Commerce.UnitTests.Catalog;

public sealed class CatalogRulesTests
{
    [Fact]
    public void Slugs_fold_turkish_characters()
    {
        Assert.Equal("sik-ust", Slug.Normalize("Şık Üst"));
        Assert.Equal("istanbul", Slug.Normalize("İstanbul"));
        Assert.Equal("cocuk-giyim", Slug.Normalize("Çocuk Giyim"));
        Assert.Null(Slug.Normalize("   "));
    }

    [Fact]
    public void Money_rejects_fractions_smaller_than_a_kurus()
    {
        Assert.True(Money.TryParse("1299.00", out var amount));
        Assert.Equal(1299.00m, amount);
        Assert.False(Money.TryParse("10.555", out _));
        Assert.False(Money.TryParse("-1.00", out _));
        Assert.False(Money.CompareAtIsValid(100m, 100m));
        Assert.True(Money.CompareAtIsValid(100m, 120m));
    }

    [Fact]
    public void Image_signatures_accept_only_jpeg_png_and_webp()
    {
        Assert.Equal("image/png", ImageSignature.DetectContentType([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]));
        Assert.Equal("image/jpeg", ImageSignature.DetectContentType([0xFF, 0xD8, 0xFF, 0xE0]));
        Assert.Null(ImageSignature.DetectContentType("plain-text-file"u8));
    }

    [Fact]
    public void Publishing_requires_a_priced_variant_and_alt_text()
    {
        var product = new Product
        {
            Name = "Tee",
            Slug = "tee",
            ShortDescription = "Short",
            Description = "Long",
            Categories = [new ProductCategory { Category = new Category { IsActive = true } }],
            Variants =
            [
                new ProductVariant
                {
                    IsActive = true,
                    Sku = "TEE-1",
                    Price = 0m,
                    CombinationKey = string.Empty,
                },
            ],
        };

        var errors = PublishRules.ForProduct(product);
        Assert.Contains("price", errors.Keys);
        Assert.Contains("media", errors.Keys);
    }

    [Fact]
    public async Task Failed_metadata_save_deletes_the_uploaded_object()
    {
        var storage = new RecordingStorage();
        var store = new ThrowingStore();
        var service = new MediaService(store, storage, TimeProvider.System, new DerivativePublisher(storage, new NoDerivatives(), Microsoft.Extensions.Logging.Abstractions.NullLogger<DerivativePublisher>.Instance));
        var productId = Guid.CreateVersion7();
        store.Product = new Product { Id = productId, Name = "Tee", Slug = "tee" };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UploadAsync(
            productId,
            new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00 },
            "Front",
            null,
            0,
            true,
            CancellationToken.None));

        Assert.Empty(storage.Objects);
    }

    private sealed class RecordingStorage : IObjectStorage
    {
        public Dictionary<string, StoredObject> Objects { get; } = [];

        public Task PutAsync(string key, ReadOnlyMemory<byte> content, string contentType, CancellationToken cancellationToken)
        {
            Objects[key] = new StoredObject(content.ToArray(), contentType);
            return Task.CompletedTask;
        }

        public Task<StoredObject?> OpenAsync(string key, CancellationToken cancellationToken) =>
            Task.FromResult(Objects.TryGetValue(key, out var value) ? value : null);

        public Task DeleteAsync(string key, CancellationToken cancellationToken)
        {
            Objects.Remove(key);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingStore : ICatalogStore
    {
        public Product? Product { get; set; }

        public Task SaveChangesAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("database unavailable");

        public Task<CatalogResult<int>> SaveProductGraphAsync(Product product, int? expectedVersion, DateTimeOffset now, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("database unavailable");

        public Task<Product?> FindProductGraphAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Product);

        public Task ClearPrimaryAsync(Guid productId, Guid exceptMediaId, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public void Add(object entity)
        {
        }

        public void Abandon()
        {
        }

        public Task<ICatalogTransaction> BeginTransactionAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public void Remove(object entity) => throw new NotSupportedException();

        public Task<Category?> FindCategoryAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> CategoryHasChildrenAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> CategoryHasProductsAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> ParentCreatesCycleAsync(Guid categoryId, Guid parentId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Collection?> FindCollectionAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> CollectionHasProductsAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> SlugTakenAsync(SlugOwner owner, string slug, Guid? exceptOwnerId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public void RetireSlug(SlugOwner owner, Guid ownerId, string currentSlug, string nextSlug, DateTimeOffset now) => throw new NotSupportedException();

        public Task<IReadOnlyList<Category>> FindCategoriesAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<Collection>> FindCollectionsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) => throw new NotSupportedException();

        public void ReplaceCategories(Product product, IReadOnlyList<Category> categories) => throw new NotSupportedException();

        public void ReplaceCollections(Product product, IReadOnlyList<Collection> collections) => throw new NotSupportedException();

        public Task<ProductOption?> FindOptionAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> OptionIsUsedAsync(Guid optionId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ProductOptionValue?> FindOptionValueAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> OptionValueIsUsedAsync(Guid optionValueId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ProductVariant?> FindVariantGraphAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> SkuTakenAsync(string sku, Guid? exceptVariantId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> BarcodeTakenAsync(string barcode, Guid? exceptVariantId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> VariantHasMovementsAsync(Guid variantId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<StockAdjustmentOutcome> AdjustStockAsync(StockAdjustment adjustment, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ProductMedia?> FindMediaAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class NoDerivatives : IImageDerivatives
    {
        public IReadOnlyList<ImageDerivative> Create(ReadOnlyMemory<byte> content, int knownWidth) => [];
    }
}
