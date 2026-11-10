using Commerce.Application.Catalog;
using Commerce.Domain.Catalog;
using Commerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using NpgsqlTypes;

namespace Commerce.Infrastructure.Catalog;

internal sealed class CatalogReader(CommerceDbContext database, Microsoft.Extensions.Options.IOptions<CatalogOptions> options) : ICatalogReader
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
                option.Values.OrderBy(value => value.Position).Select(value => new OptionValueResponse(value.Id, value.Value, value.Code, value.Position)).ToArray(),
                product.Version)).ToArray(),
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
            product.Media.OrderBy(media => media.DisplayOrder).Select(CatalogMapper.Media).ToArray());
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

        return CatalogMapper.Media(media);
    }

    public async Task<DashboardSummary> DashboardAsync(CancellationToken cancellationToken)
    {
        var threshold = Math.Max(0, options.Value.LowStockThreshold);
        var products = await database.Products.AsNoTracking()
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Total = group.Count(),
                Published = group.Count(product => product.Status == ProductStatus.Active),
                Drafts = group.Count(product => product.Status == ProductStatus.Draft),
                Archived = group.Count(product => product.Status == ProductStatus.Archived),
            })
            .FirstOrDefaultAsync(cancellationToken);
        var activeVariants = database.ProductVariants.AsNoTracking().Where(variant => variant.IsActive && variant.Balance != null);
        return new DashboardSummary(
            products?.Total ?? 0,
            products?.Published ?? 0,
            products?.Drafts ?? 0,
            products?.Archived ?? 0,
            await activeVariants.CountAsync(cancellationToken),
            await activeVariants.CountAsync(variant => variant.Balance!.OnHand - variant.Balance.Reserved <= 0, cancellationToken),
            await activeVariants.CountAsync(
                variant => variant.Balance!.OnHand - variant.Balance.Reserved > 0
                    && variant.Balance.OnHand - variant.Balance.Reserved <= threshold,
                cancellationToken),
            await database.Categories.CountAsync(cancellationToken),
            await database.Collections.CountAsync(cancellationToken),
            threshold);
    }

    public async Task<AdminProductPage> AdminProductsAsync(AdminProductQuery query, CancellationToken cancellationToken)
    {
        var products = database.Products.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Status) && Enum.TryParse<ProductStatus>(query.Status, true, out var parsed))
        {
            products = products.Where(product => product.Status == parsed);
        }

        if (query.CategoryId is Guid categoryId)
        {
            products = products.Where(product => product.Categories.Any(link => link.CategoryId == categoryId));
        }

        if (query.CollectionId is Guid collectionId)
        {
            products = products.Where(product => product.Collections.Any(link => link.CollectionId == collectionId));
        }

        if (!string.IsNullOrWhiteSpace(query.Search) && query.Search.Trim().Length >= 2)
        {
            var term = query.Search.Trim();
            products = products.Where(product =>
                EF.Property<NpgsqlTsVector>(product, "SearchVector").Matches(EF.Functions.PlainToTsQuery("turkish", term)));
        }

        var ordered = query.Sort == "name"
            ? products.OrderBy(product => product.Name).ThenBy(product => product.Id)
            : products.OrderByDescending(product => product.UpdatedAt).ThenBy(product => product.Id);
        var total = await ordered.CountAsync(cancellationToken);
        var rows = await ordered.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(product => new
            {
                product.Id,
                product.Name,
                product.Slug,
                product.Status,
                product.IsFeatured,
                product.Version,
                product.UpdatedAt,
                VariantCount = product.Variants.Count(),
                PrimaryMediaId = product.Media.Where(media => media.IsPrimary).Select(media => (Guid?)media.Id).FirstOrDefault(),
                FirstSku = product.Variants.OrderBy(variant => variant.Sku).Select(variant => variant.Sku).FirstOrDefault(),
                CategoryName = product.Categories.OrderBy(link => link.Category.Name).Select(link => link.Category.Name).FirstOrDefault(),
                MinPrice = product.Variants.Where(variant => variant.IsActive).Min(variant => (decimal?)variant.Price),
                MaxPrice = product.Variants.Where(variant => variant.IsActive).Max(variant => (decimal?)variant.Price),
                OnHand = product.Variants.Sum(variant => (int?)variant.Balance!.OnHand) ?? 0,
                Available = product.Variants.Sum(variant => (int?)(variant.Balance!.OnHand - variant.Balance.Reserved)) ?? 0,
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
                row.UpdatedAt,
                row.Version,
                row.PrimaryMediaId,
                row.VariantCount switch
                {
                    0 => null,
                    1 => row.FirstSku,
                    _ => $"{row.FirstSku} +{row.VariantCount - 1}",
                },
                row.CategoryName,
                row.MinPrice is decimal min ? Money.Format(min) : null,
                row.MaxPrice is decimal max ? Money.Format(max) : null,
                Money.Currency,
                row.OnHand,
                row.Available)).ToArray(),
            query.Page,
            query.PageSize,
            total);
    }

    public async Task<ProductResponse?> AdminProductAsync(Guid id, CancellationToken cancellationToken)
    {
        var product = await ProductGraph().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return product is null ? null : CatalogMapper.Product(product, includeStock: true);
    }

    public async Task<InventoryPage> InventoryAsync(AdminInventoryQuery query, CancellationToken cancellationToken)
    {
        var threshold = Math.Max(0, options.Value.LowStockThreshold);
        var variants = database.ProductVariants.AsNoTracking().Where(variant => variant.Balance != null);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            var normalized = Sku.Normalize(term) ?? term.ToUpperInvariant();
            variants = variants.Where(variant => variant.Sku.Contains(normalized) || variant.Product.Name.ToLower().Contains(term.ToLower()));
        }

        variants = query.Stock switch
        {
            "out" => variants.Where(variant => variant.Balance!.OnHand - variant.Balance.Reserved <= 0),
            "low" => variants.Where(variant => variant.Balance!.OnHand - variant.Balance.Reserved > 0
                && variant.Balance.OnHand - variant.Balance.Reserved <= threshold),
            "in_stock" => variants.Where(variant => variant.Balance!.OnHand - variant.Balance.Reserved > threshold),
            _ => variants,
        };

        var ordered = variants.OrderBy(variant => variant.Sku).ThenBy(variant => variant.Id);
        var total = await ordered.CountAsync(cancellationToken);
        var rows = await ordered.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(variant => new
            {
                variant.Id,
                variant.Sku,
                variant.ProductId,
                ProductName = variant.Product.Name,
                OnHand = variant.Balance!.OnHand,
                Reserved = variant.Balance.Reserved,
                Options = variant.Selections
                    .OrderBy(selection => selection.OptionValue.Option.Position)
                    .Select(selection => selection.OptionValue.Value)
                    .ToList(),
            })
            .ToListAsync(cancellationToken);
        return new InventoryPage(
            rows.Select(row => new InventoryRow(
                row.Id,
                row.Sku,
                row.ProductId,
                row.ProductName,
                row.OnHand,
                row.Reserved,
                row.OnHand - row.Reserved,
                string.Join(" / ", row.Options))).ToArray(),
            query.Page,
            query.PageSize,
            total);
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

    public async Task<MovementPage> MovementHistoryAsync(MovementHistoryQuery query, CancellationToken cancellationToken)
    {
        var movements = database.InventoryMovements.AsNoTracking();
        if (query.VariantId is Guid variantId)
        {
            movements = movements.Where(movement => movement.VariantId == variantId);
        }

        if (!string.IsNullOrWhiteSpace(query.Sku))
        {
            var normalized = Sku.Normalize(query.Sku) ?? query.Sku.Trim().ToUpperInvariant();
            movements = movements.Where(movement => movement.Variant.Sku.Contains(normalized));
        }

        var ordered = movements.OrderByDescending(movement => movement.CreatedAt).ThenByDescending(movement => movement.Id);
        var total = await ordered.CountAsync(cancellationToken);
        var rows = await ordered.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(movement => new
            {
                movement.Id,
                movement.VariantId,
                movement.Variant.ProductId,
                ProductName = movement.Variant.Product.Name,
                movement.Variant.Sku,
                movement.MovementType,
                movement.QuantityDelta,
                movement.OnHandAfter,
                movement.ReservedAfter,
                movement.Note,
                movement.ActorUserId,
                ActorEmail = database.Users.Where(user => user.Id == movement.ActorUserId).Select(user => user.Email).FirstOrDefault(),
                movement.IdempotencyKey,
                movement.CreatedAt,
                Options = movement.Variant.Selections
                    .OrderBy(selection => selection.OptionValue.Option.Position)
                    .Select(selection => selection.OptionValue.Value)
                    .ToList(),
            })
            .ToListAsync(cancellationToken);
        return new MovementPage(
            rows.Select(row => new MovementListItem(
                row.Id,
                row.VariantId,
                row.ProductId,
                row.ProductName,
                row.Sku,
                string.Join(" / ", row.Options),
                row.MovementType.ToString().ToLowerInvariant(),
                row.QuantityDelta,
                row.OnHandAfter,
                row.ReservedAfter,
                row.Note,
                row.ActorUserId,
                row.ActorEmail,
                row.IdempotencyKey,
                row.CreatedAt)).ToArray(),
            query.Page,
            query.PageSize,
            total);
    }

    public async Task<MediaResponse?> AdminMediaAsync(Guid id, CancellationToken cancellationToken)
    {
        var media = await database.ProductMedia.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return media is null ? null : CatalogMapper.Media(media);
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
