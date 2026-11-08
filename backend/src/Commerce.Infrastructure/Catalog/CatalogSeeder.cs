using Commerce.Domain.Catalog;
using Commerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Infrastructure.Catalog;

/// <summary>
/// Inserts sample catalogue rows when their slugs are missing. Existing rows are left unchanged.
/// </summary>
public sealed class CatalogSeeder(CommerceDbContext database, TimeProvider time)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var women = await EnsureCategoryAsync("Women", "women", null, 1, now, cancellationToken);
        var men = await EnsureCategoryAsync("Men", "men", null, 2, now, cancellationToken);
        await EnsureCategoryAsync("Unisex", "unisex", null, 3, now, cancellationToken);
        var tops = await EnsureCategoryAsync("Tops", "women-tops", women.Id, 1, now, cancellationToken);
        await EnsureCategoryAsync("Bottoms", "women-bottoms", women.Id, 2, now, cancellationToken);
        var arrivals = await EnsureCollectionAsync("New Arrivals", "new-arrivals", 1, now, cancellationToken);
        var training = await EnsureCollectionAsync("Training Essentials", "training-essentials", 2, now, cancellationToken);

        await EnsureShirtAsync(women, tops, arrivals, now, cancellationToken);
        await EnsureDraftShortsAsync(women, now, cancellationToken);
        await EnsureJacketAsync(men, training, now, cancellationToken);
    }

    private async Task EnsureShirtAsync(
        Category women,
        Category tops,
        Collection arrivals,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (await database.Products.AnyAsync(product => product.Slug == "performance-training-t-shirt", cancellationToken))
        {
            return;
        }

        var product = CreateProduct(
            "Performance Training T-Shirt",
            "performance-training-t-shirt",
            "A lightweight training tee.",
            "Breathable training tee for daily sessions.",
            "Sportswear",
            featured: true,
            ProductStatus.Active,
            now);
        var color = AddOption(product, "Color", "color", 1);
        var black = AddValue(color, "Black", "black", "#111111", 1);
        var white = AddValue(color, "White", "white", "#f5f5f5", 2);
        var size = AddOption(product, "Size", "size", 2);
        var small = AddValue(size, "S", "s", null, 1);
        var medium = AddValue(size, "M", "m", null, 2);
        var large = AddValue(size, "L", "l", null, 3);
        AddVariant(product, "TSHIRT-BLK-S", 1299.00m, null, 10, now, black, small);
        AddVariant(product, "TSHIRT-BLK-M", 1299.00m, null, 0, now, black, medium);
        AddVariant(product, "TSHIRT-WHT-L", 1199.00m, 1499.00m, 4, now, white, large);
        Assign(product, [women, tops], [arrivals]);
        AddMedia(product, "Black performance training t-shirt", now);
        database.Products.Add(product);
        await database.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureDraftShortsAsync(Category women, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (await database.Products.AnyAsync(product => product.Slug == "running-shorts", cancellationToken))
        {
            return;
        }

        var product = CreateProduct("Running Shorts", "running-shorts", string.Empty, string.Empty, null, false, ProductStatus.Draft, now);
        Assign(product, [women], []);
        database.Products.Add(product);
        await database.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureJacketAsync(Category men, Collection training, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (await database.Products.AnyAsync(product => product.Slug == "training-jacket", cancellationToken))
        {
            return;
        }

        var product = CreateProduct(
            "Training Jacket",
            "training-jacket",
            "A warm training jacket.",
            "Light insulation for cool morning runs.",
            "Sportswear",
            featured: false,
            ProductStatus.Active,
            now);
        var color = AddOption(product, "Color", "color", 1);
        var navy = AddValue(color, "Navy", "navy", "#1d2a44", 1);
        var size = AddOption(product, "Size", "size", 2);
        var medium = AddValue(size, "M", "m", null, 1);
        AddVariant(product, "JACKET-NVY-M", 3499.00m, null, 2, now, navy, medium);
        Assign(product, [men], [training]);
        AddMedia(product, "Navy training jacket", now);
        database.Products.Add(product);
        await database.SaveChangesAsync(cancellationToken);
    }

    private async Task<Category> EnsureCategoryAsync(
        string name,
        string slug,
        Guid? parentId,
        int order,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var existing = await database.Categories.FirstOrDefaultAsync(category => category.Slug == slug, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var category = new Category
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            Slug = slug,
            ParentCategoryId = parentId,
            DisplayOrder = order,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        database.Categories.Add(category);
        await database.SaveChangesAsync(cancellationToken);
        return category;
    }

    private async Task<Collection> EnsureCollectionAsync(
        string name,
        string slug,
        int order,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var existing = await database.Collections.FirstOrDefaultAsync(collection => collection.Slug == slug, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var collection = new Collection
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            Slug = slug,
            DisplayOrder = order,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        database.Collections.Add(collection);
        await database.SaveChangesAsync(cancellationToken);
        return collection;
    }

    private static Product CreateProduct(
        string name,
        string slug,
        string shortDescription,
        string description,
        string? brand,
        bool featured,
        ProductStatus status,
        DateTimeOffset now) => new()
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            Slug = slug,
            ShortDescription = shortDescription,
            Description = description,
            Brand = brand,
            IsFeatured = featured,
            Status = status,
            PublishedAt = status == ProductStatus.Active ? now : null,
            CreatedAt = now,
            UpdatedAt = now,
        };

    private static ProductOption AddOption(Product product, string name, string normalized, int position)
    {
        var option = new ProductOption
        {
            Id = Guid.CreateVersion7(),
            ProductId = product.Id,
            Product = product,
            Name = name,
            NormalizedName = normalized,
            Position = position,
        };
        product.Options.Add(option);
        return option;
    }

    private static ProductOptionValue AddValue(ProductOption option, string value, string normalized, string? code, int position)
    {
        var optionValue = new ProductOptionValue
        {
            Id = Guid.CreateVersion7(),
            OptionId = option.Id,
            Option = option,
            Value = value,
            NormalizedValue = normalized,
            Code = code,
            Position = position,
        };
        option.Values.Add(optionValue);
        return optionValue;
    }

    private void AddVariant(
        Product product,
        string sku,
        decimal price,
        decimal? compareAt,
        int onHand,
        DateTimeOffset now,
        params ProductOptionValue[] values)
    {
        var variant = new ProductVariant
        {
            Id = Guid.CreateVersion7(),
            ProductId = product.Id,
            Product = product,
            Sku = sku,
            Price = price,
            CompareAtPrice = compareAt,
            CurrencyCode = Money.Currency,
            IsActive = true,
            CombinationKey = CombinationKey.Build(values.Select(value => value.Id)),
            CreatedAt = now,
            UpdatedAt = now,
            Balance = new InventoryBalance { OnHand = onHand, Reserved = 0 },
        };
        variant.Balance.VariantId = variant.Id;
        variant.Balance.Variant = variant;
        foreach (var value in values)
        {
            variant.Selections.Add(new VariantOptionValue
            {
                VariantId = variant.Id,
                Variant = variant,
                OptionValueId = value.Id,
                OptionValue = value,
            });
        }

        product.Variants.Add(variant);
        if (onHand <= 0)
        {
            return;
        }

        database.InventoryMovements.Add(new InventoryMovement
        {
            Id = Guid.CreateVersion7(),
            VariantId = variant.Id,
            Variant = variant,
            MovementType = InventoryMovementType.InitialStock,
            QuantityDelta = onHand,
            OnHandAfter = onHand,
            ReservedAfter = 0,
            IdempotencyKey = $"initial-{variant.Id:N}",
            RequestHash = "seed",
            CreatedAt = now,
        });
    }

    private static void Assign(Product product, IReadOnlyList<Category> categories, IReadOnlyList<Collection> collections)
    {
        foreach (var category in categories)
        {
            product.Categories.Add(new ProductCategory
            {
                ProductId = product.Id,
                Product = product,
                CategoryId = category.Id,
                Category = category,
            });
        }

        foreach (var collection in collections)
        {
            product.Collections.Add(new ProductCollection
            {
                ProductId = product.Id,
                Product = product,
                CollectionId = collection.Id,
                Collection = collection,
            });
        }
    }

    private static void AddMedia(Product product, string altText, DateTimeOffset now)
    {
        var media = new ProductMedia
        {
            Id = Guid.CreateVersion7(),
            ProductId = product.Id,
            Product = product,
            AltText = altText,
            DisplayOrder = 0,
            IsPrimary = true,
            ContentType = "image/jpeg",
            FileSize = 0,
            CreatedAt = now,
        };
        media.ObjectKey = $"catalog/{product.Id}/{media.Id}.jpg";
        product.Media.Add(media);
    }
}
