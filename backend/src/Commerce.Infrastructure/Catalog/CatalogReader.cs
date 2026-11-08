using Commerce.Application.Catalog;
using Commerce.Domain.Catalog;
using Commerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using NpgsqlTypes;

namespace Commerce.Infrastructure.Catalog;

internal sealed class CatalogReader(CommerceDbContext database) : ICatalogReader
{
    public async Task<IReadOnlyList<CategoryNode>> CategoryTreeAsync(bool activeOnly, CancellationToken cancellationToken)
    {
        var rows = await Categories(activeOnly).ToListAsync(cancellationToken);
        return BuildTree(rows);
    }

    public async Task<CategoryNode?> CategoryBySlugAsync(string slug, bool activeOnly, CancellationToken cancellationToken)
    {
        var id = await ResolveAsync(SlugOwner.Category, slug, cancellationToken);
        if (id is null)
        {
            return null;
        }

        var category = await database.Categories.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (category is null || (activeOnly && !category.IsActive))
        {
            return null;
        }

        var rows = await Categories(activeOnly).ToListAsync(cancellationToken);
        return BuildTree(rows).SelectMany(Flatten).FirstOrDefault(node => node.Id == category.Id)
            ?? new CategoryNode(category.Id, category.Name, category.Slug, category.Description, category.DisplayOrder, category.IsActive, []);
    }

    public async Task<IReadOnlyList<CollectionResponse>> CollectionsAsync(bool activeOnly, CancellationToken cancellationToken)
    {
        var query = database.Collections.AsNoTracking();
        if (activeOnly)
        {
            query = query.Where(collection => collection.IsActive);
        }

        var rows = await query.OrderBy(collection => collection.DisplayOrder).ThenBy(collection => collection.Name).ToListAsync(cancellationToken);
        return rows.Select(CatalogMapper.Collection).ToArray();
    }

    public async Task<CollectionResponse?> CollectionBySlugAsync(string slug, bool activeOnly, CancellationToken cancellationToken)
    {
        var id = await ResolveAsync(SlugOwner.Collection, slug, cancellationToken);
        if (id is null)
        {
            return null;
        }

        var collection = await database.Collections.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (collection is null || (activeOnly && !collection.IsActive))
        {
            return null;
        }

        return CatalogMapper.Collection(collection);
    }

    public async Task<PublicProductPageResult> PublicProductsAsync(PublicProductQuery query, CancellationToken cancellationToken)
    {
        var products = database.Products.AsNoTracking().Where(product => product.Status == ProductStatus.Active);
        if (!string.IsNullOrWhiteSpace(query.CategorySlug))
        {
            var ids = await ActiveCategoryIdsAsync(query.CategorySlug, cancellationToken);
            if (ids is null)
            {
                return new PublicProductPageResult(null, true, false);
            }

            products = products.Where(product => product.Categories.Any(link => ids.Contains(link.CategoryId)));
        }

        if (!string.IsNullOrWhiteSpace(query.CollectionSlug))
        {
            var id = await ResolveAsync(SlugOwner.Collection, query.CollectionSlug, cancellationToken);
            var collection = id is null
                ? null
                : await database.Collections.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (collection is null || !collection.IsActive)
            {
                return new PublicProductPageResult(null, false, true);
            }

            products = products.Where(product => product.Collections.Any(link => link.CollectionId == collection.Id));
        }

        var color = Slug.Normalize(query.Color);
        if (!string.IsNullOrWhiteSpace(query.Color))
        {
            if (color is null)
            {
                return new PublicProductPageResult(new ProductPage([], query.Page, query.PageSize, 0), false, false);
            }

            products = products.Where(product => product.Variants.Any(variant =>
                variant.IsActive && variant.Selections.Any(selection =>
                    selection.OptionValue.Option.NormalizedName == "color"
                    && selection.OptionValue.NormalizedValue == color)));
        }

        var size = Slug.Normalize(query.Size);
        if (!string.IsNullOrWhiteSpace(query.Size))
        {
            if (size is null)
            {
                return new PublicProductPageResult(new ProductPage([], query.Page, query.PageSize, 0), false, false);
            }

            products = products.Where(product => product.Variants.Any(variant =>
                variant.IsActive && variant.Selections.Any(selection =>
                    selection.OptionValue.Option.NormalizedName == "size"
                    && selection.OptionValue.NormalizedValue == size)));
        }

        if (query.MinPrice is decimal minPrice)
        {
            products = products.Where(product => product.Variants.Any(variant => variant.IsActive && variant.Price >= minPrice));
        }

        if (query.MaxPrice is decimal maxPrice)
        {
            products = products.Where(product => product.Variants.Any(variant => variant.IsActive && variant.Price <= maxPrice));
        }

        if (query.AvailableOnly)
        {
            products = products.Where(product => product.Variants.Any(variant =>
                variant.IsActive && variant.Balance != null && variant.Balance.OnHand > variant.Balance.Reserved));
        }

        if (!string.IsNullOrWhiteSpace(query.Search) && query.Search.Trim().Length >= 2)
        {
            var term = query.Search.Trim();
            products = products.Where(product =>
                EF.Property<NpgsqlTsVector>(product, "SearchVector").Matches(EF.Functions.PlainToTsQuery("turkish", term)));
        }

        products = query.Sort switch
        {
            ProductSort.PriceAscending => products
                .OrderBy(product => product.Variants.Where(variant => variant.IsActive).Min(variant => (decimal?)variant.Price) ?? decimal.MaxValue)
                .ThenBy(product => product.Id),
            ProductSort.PriceDescending => products
                .OrderByDescending(product => product.Variants.Where(variant => variant.IsActive).Min(variant => (decimal?)variant.Price) ?? 0m)
                .ThenBy(product => product.Id),
            _ => products.OrderByDescending(product => product.PublishedAt).ThenByDescending(product => product.Id),
        };

        var total = await products.CountAsync(cancellationToken);
        var rows = await products
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(product => new
            {
                product.Id,
                product.Name,
                product.Slug,
                product.ShortDescription,
                product.Brand,
                product.IsFeatured,
                FromPrice = product.Variants.Where(variant => variant.IsActive).Min(variant => (decimal?)variant.Price) ?? 0m,
                Available = product.Variants.Any(variant => variant.IsActive && variant.Balance != null && variant.Balance.OnHand > variant.Balance.Reserved),
                PrimaryMediaId = product.Media.Where(media => media.IsPrimary).Select(media => (Guid?)media.Id).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);
        var cards = rows.Select(row => new PublicProductCard(
            row.Id,
            row.Name,
            row.Slug,
            row.ShortDescription,
            row.Brand,
            row.IsFeatured,
            Money.Format(row.FromPrice),
            Money.Currency,
            row.Available,
            row.PrimaryMediaId)).ToArray();

        return new PublicProductPageResult(new ProductPage(cards, query.Page, query.PageSize, total), false, false);
    }

    public async Task<PublicProductDetail?> PublicProductAsync(string slug, CancellationToken cancellationToken)
    {
        var id = await ResolveAsync(SlugOwner.Product, slug, cancellationToken);
        if (id is null)
        {
            return null;
        }

        var product = await ProductGraph().FirstOrDefaultAsync(item => item.Id == id && item.Status == ProductStatus.Active, cancellationToken);
        if (product is null)
        {
            return null;
        }

        var activeVariants = product.Variants.Where(variant => variant.IsActive).ToArray();
        var fromPrice = activeVariants.Length == 0 ? 0m : activeVariants.Min(variant => variant.Price);
        return new PublicProductDetail(
            product.Id,
            product.Name,
            product.Slug,
            product.ShortDescription,
            product.Description,
            product.Brand,
            product.IsFeatured,
            product.PublishedAt,
            Money.Format(fromPrice),
            Money.Currency,
            activeVariants.Any(variant => variant.Balance is { Available: > 0 }),
            product.Categories.Where(link => link.Category.IsActive).Select(link => new NamedSlug(link.Category.Id, link.Category.Name, link.Category.Slug)).ToArray(),
            product.Collections.Where(link => link.Collection.IsActive).Select(link => new NamedSlug(link.Collection.Id, link.Collection.Name, link.Collection.Slug)).ToArray(),
            product.Options.OrderBy(option => option.Position).Select(option => new OptionResponse(
                option.Id,
                option.Name,
                option.Position,
                option.Values.OrderBy(value => value.Position).Select(value => new OptionValueResponse(value.Id, value.Value, value.Code, value.Position)).ToArray())).ToArray(),
            activeVariants.OrderBy(variant => variant.Sku).Select(variant => new PublicVariantResponse(
                variant.Id,
                variant.Sku,
                variant.IsActive,
                Money.Format(variant.Price),
                variant.CompareAtPrice is decimal compareAt ? Money.Format(compareAt) : null,
                variant.CurrencyCode,
                variant.Balance is { Available: > 0 },
                variant.Selections.OrderBy(selection => selection.OptionValue.Option.Position)
                    .Select(selection => new SelectedOptionResponse(selection.OptionValue.Option.Name, selection.OptionValue.Value, selection.OptionValue.Code))
                    .ToArray())).ToArray(),
            product.Media.OrderBy(media => media.DisplayOrder).Select(media => new MediaResponse(
                media.Id,
                media.VariantId,
                media.AltText,
                media.DisplayOrder,
                media.IsPrimary,
                media.ContentType,
                media.FileSize,
                CatalogMapper.MediaPath(media.Id))).ToArray());
    }

    public async Task<MediaResponse?> PublicMediaAsync(Guid id, CancellationToken cancellationToken)
    {
        var media = await database.ProductMedia.AsNoTracking()
            .Include(item => item.Product)
            .Include(item => item.Variant)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (media is null || media.Product.Status != ProductStatus.Active || media.Variant is { IsActive: false })
        {
            return null;
        }

        return new MediaResponse(media.Id, media.VariantId, media.AltText, media.DisplayOrder, media.IsPrimary, media.ContentType, media.FileSize, CatalogMapper.MediaPath(media.Id));
    }

    public async Task<AdminProductPage> AdminProductsAsync(int page, int pageSize, string? status, CancellationToken cancellationToken)
    {
        var products = database.Products.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ProductStatus>(status, true, out var parsed))
        {
            products = products.Where(product => product.Status == parsed);
        }

        var ordered = products.OrderByDescending(product => product.UpdatedAt).ThenBy(product => product.Id);
        var total = await ordered.CountAsync(cancellationToken);
        var rows = await ordered.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(product => new
            {
                product.Id,
                product.Name,
                product.Slug,
                product.Status,
                product.IsFeatured,
                VariantCount = product.Variants.Count(),
                product.UpdatedAt,
            })
            .ToListAsync(cancellationToken);
        return new AdminProductPage(
            rows.Select(row => new AdminProductCard(
                row.Id,
                row.Name,
                row.Slug,
                row.Status.ToString().ToLowerInvariant(),
                row.IsFeatured,
                row.VariantCount,
                row.UpdatedAt)).ToArray(),
            page,
            pageSize,
            total);
    }

    public async Task<ProductResponse?> AdminProductAsync(Guid id, CancellationToken cancellationToken)
    {
        var product = await ProductGraph().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return product is null ? null : CatalogMapper.Product(product, includeStock: true);
    }

    public async Task<InventoryPage> InventoryAsync(int page, int pageSize, string? sku, CancellationToken cancellationToken)
    {
        var variants = database.ProductVariants.AsNoTracking().Where(variant => variant.Balance != null);
        if (!string.IsNullOrWhiteSpace(sku))
        {
            var normalized = Sku.Normalize(sku) ?? sku.Trim().ToUpperInvariant();
            variants = variants.Where(variant => variant.Sku.Contains(normalized));
        }

        var ordered = variants.OrderBy(variant => variant.Sku).ThenBy(variant => variant.Id);
        var total = await ordered.CountAsync(cancellationToken);
        var rows = await ordered.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(variant => new InventoryRow(
                variant.Id,
                variant.Sku,
                variant.ProductId,
                variant.Product.Name,
                variant.Balance!.OnHand,
                variant.Balance.Reserved,
                variant.Balance.OnHand - variant.Balance.Reserved))
            .ToListAsync(cancellationToken);
        return new InventoryPage(rows, page, pageSize, total);
    }

    public async Task<IReadOnlyList<MovementResponse>?> MovementsAsync(Guid variantId, CancellationToken cancellationToken)
    {
        var exists = await database.ProductVariants.AnyAsync(variant => variant.Id == variantId, cancellationToken);
        if (!exists)
        {
            return null;
        }

        var rows = await database.InventoryMovements.AsNoTracking()
            .Where(movement => movement.VariantId == variantId)
            .OrderByDescending(movement => movement.CreatedAt)
            .ThenByDescending(movement => movement.Id)
            .Take(100)
            .ToListAsync(cancellationToken);
        return rows.Select(CatalogMapper.Movement).ToArray();
    }

    public async Task<MediaResponse?> AdminMediaAsync(Guid id, CancellationToken cancellationToken)
    {
        var media = await database.ProductMedia.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return media is null
            ? null
            : new MediaResponse(media.Id, media.VariantId, media.AltText, media.DisplayOrder, media.IsPrimary, media.ContentType, media.FileSize, CatalogMapper.MediaPath(media.Id));
    }

    private IQueryable<Category> Categories(bool activeOnly)
    {
        var query = database.Categories.AsNoTracking();
        return activeOnly ? query.Where(category => category.IsActive) : query;
    }

    private IQueryable<Product> ProductGraph() =>
        database.Products.AsNoTracking().AsSplitQuery()
            .Include(product => product.Categories).ThenInclude(link => link.Category)
            .Include(product => product.Collections).ThenInclude(link => link.Collection)
            .Include(product => product.Options).ThenInclude(option => option.Values)
            .Include(product => product.Variants).ThenInclude(variant => variant.Selections).ThenInclude(selection => selection.OptionValue).ThenInclude(value => value.Option)
            .Include(product => product.Variants).ThenInclude(variant => variant.Balance)
            .Include(product => product.Media);

    private async Task<List<Guid>?> ActiveCategoryIdsAsync(string slug, CancellationToken cancellationToken)
    {
        var id = await ResolveAsync(SlugOwner.Category, slug, cancellationToken);
        if (id is null)
        {
            return null;
        }

        var category = await database.Categories.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (category is null || !category.IsActive)
        {
            return null;
        }

        return await database.Database.SqlQuery<Guid>(
            $"""
            WITH RECURSIVE descendants AS (
                SELECT id FROM categories WHERE id = {category.Id} AND is_active
                UNION ALL
                SELECT child.id
                FROM categories AS child
                INNER JOIN descendants AS parent ON child.parent_category_id = parent.id
                WHERE child.is_active
            )
            SELECT id AS "Value" FROM descendants
            """).ToListAsync(cancellationToken);
    }

    private async Task<Guid?> ResolveAsync(SlugOwner owner, string slug, CancellationToken cancellationToken)
    {
        var normalized = Slug.Normalize(slug);
        if (normalized is null)
        {
            return null;
        }

        Guid? current = owner switch
        {
            SlugOwner.Category => await database.Categories.Where(category => category.Slug == normalized).Select(category => (Guid?)category.Id).FirstOrDefaultAsync(cancellationToken),
            SlugOwner.Collection => await database.Collections.Where(collection => collection.Slug == normalized).Select(collection => (Guid?)collection.Id).FirstOrDefaultAsync(cancellationToken),
            _ => await database.Products.Where(product => product.Slug == normalized).Select(product => (Guid?)product.Id).FirstOrDefaultAsync(cancellationToken),
        };
        if (current is not null)
        {
            return current;
        }

        return await database.SlugHistories
            .Where(entry => entry.OwnerType == owner && entry.Slug == normalized)
            .Select(entry => (Guid?)entry.OwnerId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static IReadOnlyList<CategoryNode> BuildTree(IReadOnlyList<Category> rows)
    {
        var ids = rows.Select(category => category.Id).ToHashSet();
        CategoryNode Node(Category category) => new(
            category.Id,
            category.Name,
            category.Slug,
            category.Description,
            category.DisplayOrder,
            category.IsActive,
            rows.Where(child => child.ParentCategoryId == category.Id)
                .OrderBy(child => child.DisplayOrder)
                .ThenBy(child => child.Name)
                .Select(Node)
                .ToArray());

        return rows
            .Where(category => category.ParentCategoryId is null || !ids.Contains(category.ParentCategoryId.Value))
            .OrderBy(category => category.DisplayOrder)
            .ThenBy(category => category.Name)
            .Select(Node)
            .ToArray();
    }

    private static IEnumerable<CategoryNode> Flatten(CategoryNode node)
    {
        yield return node;
        foreach (var child in node.Children.SelectMany(Flatten))
        {
            yield return child;
        }
    }
}
