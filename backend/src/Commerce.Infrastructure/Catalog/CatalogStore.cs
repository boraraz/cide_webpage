using Commerce.Application.Catalog;
using Commerce.Domain.Catalog;
using Commerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Commerce.Infrastructure.Catalog;

internal sealed class CatalogStore(CommerceDbContext database, TimeProvider time) : ICatalogStore
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            if (FindPostgres(exception) is PostgresException postgres)
            {
                throw MapConstraint(postgres);
            }

            throw;
        }
    }

    public async Task<ICatalogTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        return new CatalogTransaction(transaction);
    }

    public void Abandon() => database.ChangeTracker.Clear();

    public void Add(object entity) => database.Add(entity);

    public void Remove(object entity) => database.Remove(entity);

    public Task<Category?> FindCategoryAsync(Guid id, CancellationToken cancellationToken) =>
        database.Categories.FirstOrDefaultAsync(category => category.Id == id, cancellationToken);

    public Task<bool> CategoryHasChildrenAsync(Guid id, CancellationToken cancellationToken) =>
        database.Categories.AnyAsync(category => category.ParentCategoryId == id, cancellationToken);

    public Task<bool> CategoryHasProductsAsync(Guid id, CancellationToken cancellationToken) =>
        database.ProductCategories.AnyAsync(link => link.CategoryId == id, cancellationToken);

    public async Task<bool> ParentCreatesCycleAsync(Guid categoryId, Guid parentId, CancellationToken cancellationToken)
    {
        if (categoryId == parentId)
        {
            return true;
        }

        var matches = await database.Database.SqlQuery<int>(
            $"""
            WITH RECURSIVE descendants AS (
                SELECT id FROM categories WHERE parent_category_id = {categoryId}
                UNION ALL
                SELECT child.id
                FROM categories AS child
                INNER JOIN descendants ON child.parent_category_id = descendants.id
            )
            SELECT 1 AS "Value" FROM descendants WHERE id = {parentId} LIMIT 1
            """).ToListAsync(cancellationToken);
        return matches.Count > 0;
    }

    public Task<Collection?> FindCollectionAsync(Guid id, CancellationToken cancellationToken) =>
        database.Collections.FirstOrDefaultAsync(collection => collection.Id == id, cancellationToken);

    public Task<bool> CollectionHasProductsAsync(Guid id, CancellationToken cancellationToken) =>
        database.ProductCollections.AnyAsync(link => link.CollectionId == id, cancellationToken);

    public async Task<bool> SlugTakenAsync(SlugOwner owner, string slug, Guid? exceptOwnerId, CancellationToken cancellationToken)
    {
        var history = database.SlugHistories.Where(entry => entry.OwnerType == owner && entry.Slug == slug);
        if (exceptOwnerId is Guid ownerId)
        {
            history = history.Where(entry => entry.OwnerId != ownerId);
        }

        if (await history.AnyAsync(cancellationToken))
        {
            return true;
        }

        return owner switch
        {
            SlugOwner.Category => await SlugExists(database.Categories.Where(category => category.Slug == slug), exceptOwnerId, cancellationToken),
            SlugOwner.Collection => await SlugExists(database.Collections.Where(collection => collection.Slug == slug), exceptOwnerId, cancellationToken),
            _ => await SlugExists(database.Products.Where(product => product.Slug == slug), exceptOwnerId, cancellationToken),
        };
    }

    public void RetireSlug(SlugOwner owner, Guid ownerId, string currentSlug, string nextSlug, DateTimeOffset now)
    {
        if (currentSlug == nextSlug)
        {
            return;
        }

        var reclaim = database.SlugHistories.Where(entry =>
            entry.OwnerType == owner && entry.OwnerId == ownerId && entry.Slug == nextSlug);
        database.SlugHistories.RemoveRange(reclaim);
        database.SlugHistories.Add(new SlugHistory
        {
            Id = Guid.CreateVersion7(),
            OwnerType = owner,
            OwnerId = ownerId,
            Slug = currentSlug,
            CreatedAt = now,
        });
    }

    public Task<Product?> FindProductGraphAsync(Guid id, CancellationToken cancellationToken) =>
        database.Products
            .Include(product => product.Categories).ThenInclude(link => link.Category)
            .Include(product => product.Collections).ThenInclude(link => link.Collection)
            .Include(product => product.Options).ThenInclude(option => option.Values)
            .Include(product => product.Variants).ThenInclude(variant => variant.Selections).ThenInclude(selection => selection.OptionValue).ThenInclude(value => value.Option)
            .Include(product => product.Variants).ThenInclude(variant => variant.Balance)
            .Include(product => product.Media)
            .FirstOrDefaultAsync(product => product.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Category>> FindCategoriesAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        return await database.Categories.Where(category => ids.Contains(category.Id)).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Collection>> FindCollectionsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        return await database.Collections.Where(collection => ids.Contains(collection.Id)).ToListAsync(cancellationToken);
    }

    public void ReplaceCategories(Product product, IReadOnlyList<Category> categories)
    {
        database.ProductCategories.RemoveRange(product.Categories);
        product.Categories.Clear();
        foreach (var category in categories)
        {
            var link = new ProductCategory { ProductId = product.Id, CategoryId = category.Id, Product = product, Category = category };
            product.Categories.Add(link);
            database.ProductCategories.Add(link);
        }
    }

    public void ReplaceCollections(Product product, IReadOnlyList<Collection> collections)
    {
        database.ProductCollections.RemoveRange(product.Collections);
        product.Collections.Clear();
        foreach (var collection in collections)
        {
            var link = new ProductCollection { ProductId = product.Id, CollectionId = collection.Id, Product = product, Collection = collection };
            product.Collections.Add(link);
            database.ProductCollections.Add(link);
        }
    }

    public Task<ProductOption?> FindOptionAsync(Guid id, CancellationToken cancellationToken) =>
        database.ProductOptions.Include(option => option.Values).FirstOrDefaultAsync(option => option.Id == id, cancellationToken);

    public Task<bool> OptionIsUsedAsync(Guid optionId, CancellationToken cancellationToken) =>
        database.VariantOptionValues.AnyAsync(selection => selection.OptionValue.OptionId == optionId, cancellationToken);

    public Task<ProductOptionValue?> FindOptionValueAsync(Guid id, CancellationToken cancellationToken) =>
        database.ProductOptionValues.FirstOrDefaultAsync(value => value.Id == id, cancellationToken);

    public Task<bool> OptionValueIsUsedAsync(Guid optionValueId, CancellationToken cancellationToken) =>
        database.VariantOptionValues.AnyAsync(selection => selection.OptionValueId == optionValueId, cancellationToken);

    public Task<ProductVariant?> FindVariantGraphAsync(Guid id, CancellationToken cancellationToken) =>
        database.ProductVariants
            .Include(variant => variant.Product).ThenInclude(product => product.Categories).ThenInclude(link => link.Category)
            .Include(variant => variant.Product).ThenInclude(product => product.Options).ThenInclude(option => option.Values)
            .Include(variant => variant.Product).ThenInclude(product => product.Variants).ThenInclude(item => item.Selections).ThenInclude(selection => selection.OptionValue)
            .Include(variant => variant.Product).ThenInclude(product => product.Media)
            .Include(variant => variant.Selections).ThenInclude(selection => selection.OptionValue).ThenInclude(value => value.Option)
            .Include(variant => variant.Balance)
            .FirstOrDefaultAsync(variant => variant.Id == id, cancellationToken);

    public Task<bool> SkuTakenAsync(string sku, Guid? exceptVariantId, CancellationToken cancellationToken)
    {
        var query = database.ProductVariants.Where(variant => variant.Sku == sku);
        if (exceptVariantId is Guid variantId)
        {
            query = query.Where(variant => variant.Id != variantId);
        }

        return query.AnyAsync(cancellationToken);
    }

    public Task<bool> BarcodeTakenAsync(string barcode, Guid? exceptVariantId, CancellationToken cancellationToken)
    {
        var query = database.ProductVariants.Where(variant => variant.Barcode == barcode);
        if (exceptVariantId is Guid variantId)
        {
            query = query.Where(variant => variant.Id != variantId);
        }

        return query.AnyAsync(cancellationToken);
    }

    public Task<bool> VariantHasMovementsAsync(Guid variantId, CancellationToken cancellationToken) =>
        database.InventoryMovements.AnyAsync(movement => movement.VariantId == variantId, cancellationToken);

    public async Task<StockAdjustmentOutcome> AdjustStockAsync(StockAdjustment adjustment, CancellationToken cancellationToken)
    {
        var hash = CatalogSupport.HashAdjustment(adjustment.MovementType, adjustment.Quantity, adjustment.Note);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var locked = await database.Database.SqlQuery<int>(
                $"""
                SELECT 1 AS "Value"
                FROM inventory_balances
                WHERE variant_id = {adjustment.VariantId}
                FOR UPDATE
                """).ToListAsync(cancellationToken);
            if (locked.Count == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new StockAdjustmentOutcome.Missing();
            }

            var existing = await database.InventoryMovements.FirstOrDefaultAsync(
                movement => movement.VariantId == adjustment.VariantId && movement.IdempotencyKey == adjustment.IdempotencyKey,
                cancellationToken);
            if (existing is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return existing.RequestHash == hash
                    ? new StockAdjustmentOutcome.Applied(existing)
                    : new StockAdjustmentOutcome.IdempotencyConflict(existing);
            }

            var balance = await database.InventoryBalances.SingleAsync(
                item => item.VariantId == adjustment.VariantId,
                cancellationToken);
            var delta = adjustment.MovementType == InventoryMovementType.ManualIncrease
                ? adjustment.Quantity
                : -adjustment.Quantity;
            if (balance.OnHand + delta < balance.Reserved)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new StockAdjustmentOutcome.Insufficient();
            }

            balance.OnHand += delta;
            var movement = new InventoryMovement
            {
                Id = Guid.CreateVersion7(),
                VariantId = adjustment.VariantId,
                MovementType = adjustment.MovementType,
                QuantityDelta = delta,
                OnHandAfter = balance.OnHand,
                ReservedAfter = balance.Reserved,
                Note = adjustment.Note,
                ActorUserId = adjustment.ActorUserId,
                IdempotencyKey = adjustment.IdempotencyKey,
                RequestHash = hash,
                CreatedAt = time.GetUtcNow(),
            };
            database.InventoryMovements.Add(movement);
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new StockAdjustmentOutcome.Applied(movement);
        }
        catch (DbUpdateException exception) when (FindPostgres(exception)?.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await transaction.RollbackAsync(cancellationToken);
            database.ChangeTracker.Clear();
            var winner = await database.InventoryMovements.AsNoTracking().FirstOrDefaultAsync(
                movement => movement.VariantId == adjustment.VariantId && movement.IdempotencyKey == adjustment.IdempotencyKey,
                cancellationToken);
            if (winner is null)
            {
                throw;
            }

            return winner.RequestHash == hash
                ? new StockAdjustmentOutcome.Applied(winner)
                : new StockAdjustmentOutcome.IdempotencyConflict(winner);
        }
    }

    public Task<ProductMedia?> FindMediaAsync(Guid id, CancellationToken cancellationToken) =>
        database.ProductMedia
            .Include(media => media.Product)
            .Include(media => media.Variant)
            .FirstOrDefaultAsync(media => media.Id == id, cancellationToken);

    public async Task ClearPrimaryAsync(Guid productId, Guid exceptMediaId, CancellationToken cancellationToken)
    {
        var current = await database.ProductMedia
            .Where(media => media.ProductId == productId && media.IsPrimary && media.Id != exceptMediaId)
            .ToListAsync(cancellationToken);
        foreach (var media in current)
        {
            media.IsPrimary = false;
        }
    }

    private static async Task<bool> SlugExists<T>(IQueryable<T> query, Guid? exceptOwnerId, CancellationToken cancellationToken)
        where T : class
    {
        if (exceptOwnerId is Guid ownerId)
        {
            query = query.Where(entity => EF.Property<Guid>(entity, "Id") != ownerId);
        }

        return await query.AnyAsync(cancellationToken);
    }

    private static PostgresException? FindPostgres(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres)
            {
                return postgres;
            }
        }

        return null;
    }

    private static CatalogConflictException MapConstraint(PostgresException postgres)
    {
        var name = $"{postgres.ConstraintName} {postgres.MessageText}";
        if (name.Contains("category cycle", StringComparison.OrdinalIgnoreCase))
        {
            return new CatalogConflictException("parentCategoryId", "A category cannot be placed inside itself.");
        }

        if (name.Contains("sku", StringComparison.OrdinalIgnoreCase))
        {
            return new CatalogConflictException("sku", "That SKU is already in use.");
        }

        if (name.Contains("barcode", StringComparison.OrdinalIgnoreCase))
        {
            return new CatalogConflictException("barcode", "That barcode is already in use.");
        }

        if (name.Contains("combination", StringComparison.OrdinalIgnoreCase))
        {
            return new CatalogConflictException("optionValueIds", "That combination already exists.");
        }

        if (name.Contains("price", StringComparison.OrdinalIgnoreCase) || name.Contains("compare_at", StringComparison.OrdinalIgnoreCase))
        {
            return new CatalogConflictException("price", "Enter a valid price.", 400);
        }

        if (name.Contains("slug", StringComparison.OrdinalIgnoreCase))
        {
            return new CatalogConflictException("slug", "That slug is already in use.");
        }

        return new CatalogConflictException("request", "That value is already in use.");
    }

    private sealed class CatalogTransaction(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction) : ICatalogTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
