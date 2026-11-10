using Commerce.Domain.Catalog;

namespace Commerce.Application.Catalog;

public sealed class CollectionService(ICatalogStore store, TimeProvider time)
{
    public async Task<CatalogResult<CollectionResponse>> CreateAsync(
        string? name,
        string? slug,
        string? description,
        int displayOrder,
        bool isActive,
        CancellationToken cancellationToken)
    {
        var errors = CatalogSupport.Text("name", name, PublishRules.CategoryNameMaxLength, required: true);
        foreach (var (key, value) in CatalogSupport.Text("description", description, PublishRules.DescriptionOptionalMaxLength, required: false))
        {
            errors[key] = value;
        }

        if (errors.Count > 0)
        {
            return CatalogResult<CollectionResponse>.Invalid(errors);
        }

        var resolved = await CatalogSupport.ResolveSlugAsync(store, SlugOwner.Collection, slug, name!, null, cancellationToken);
        if (resolved is null)
        {
            return CatalogResult<CollectionResponse>.Invalid(new Dictionary<string, string[]> { ["slug"] = ["Enter a valid slug."] });
        }

        if (resolved.Length == 0)
        {
            return CatalogResult<CollectionResponse>.Conflict("slug", "That slug is already in use.");
        }

        var now = time.GetUtcNow();
        var collection = new Collection
        {
            Id = Guid.CreateVersion7(),
            Name = name!.Trim(),
            Slug = resolved,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            DisplayOrder = displayOrder,
            IsActive = isActive,
            CreatedAt = now,
            UpdatedAt = now,
        };
        store.Add(collection);
        return await CatalogSupport.SaveAsync(store, () => CatalogMapper.Collection(collection), cancellationToken);
    }

    public async Task<CatalogResult<CollectionResponse>> UpdateAsync(
        Guid id,
        string? name,
        string? slug,
        string? description,
        int? displayOrder,
        bool? isActive,
        CancellationToken cancellationToken)
    {
        var collection = await store.FindCollectionAsync(id, cancellationToken);
        if (collection is null)
        {
            return CatalogResult<CollectionResponse>.NotFound();
        }

        var errors = new Dictionary<string, string[]>();
        if (name is not null)
        {
            foreach (var (key, value) in CatalogSupport.Text("name", name, PublishRules.CategoryNameMaxLength, required: true))
            {
                errors[key] = value;
            }
        }

        if (description is not null)
        {
            foreach (var (key, value) in CatalogSupport.Text("description", description, PublishRules.DescriptionOptionalMaxLength, required: false))
            {
                errors[key] = value;
            }
        }

        if (errors.Count > 0)
        {
            return CatalogResult<CollectionResponse>.Invalid(errors);
        }

        if (slug is not null || name is not null)
        {
            var resolved = await CatalogSupport.ResolveSlugAsync(
                store,
                SlugOwner.Collection,
                slug ?? collection.Slug,
                name ?? collection.Name,
                collection.Id,
                cancellationToken);
            if (resolved is null)
            {
                return CatalogResult<CollectionResponse>.Invalid(new Dictionary<string, string[]> { ["slug"] = ["Enter a valid slug."] });
            }

            if (resolved.Length == 0)
            {
                return CatalogResult<CollectionResponse>.Conflict("slug", "That slug is already in use.");
            }

            store.RetireSlug(SlugOwner.Collection, collection.Id, collection.Slug, resolved, time.GetUtcNow());
            collection.Slug = resolved;
        }

        if (name is not null)
        {
            collection.Name = name.Trim();
        }

        if (description is not null)
        {
            collection.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        }

        if (displayOrder is int order)
        {
            collection.DisplayOrder = order;
        }

        if (isActive is bool active)
        {
            collection.IsActive = active;
        }

        collection.UpdatedAt = time.GetUtcNow();
        return await CatalogSupport.SaveAsync(store, () => CatalogMapper.Collection(collection), cancellationToken);
    }

    public async Task<CatalogResult<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var collection = await store.FindCollectionAsync(id, cancellationToken);
        if (collection is null)
        {
            return CatalogResult<bool>.NotFound();
        }

        if (await store.CollectionHasProductsAsync(id, cancellationToken))
        {
            return CatalogResult<bool>.Conflict("collection", "Remove product assignments before deleting this collection.");
        }

        store.Remove(collection);
        return await CatalogSupport.SaveAsync(store, () => true, cancellationToken);
    }

    public async Task<CatalogResult<bool>> AssignProductAsync(
        Guid collectionId,
        Guid productId,
        CancellationToken cancellationToken,
        int? expectedVersion = null)
    {
        var collection = await store.FindCollectionAsync(collectionId, cancellationToken);
        var product = await store.FindProductGraphAsync(productId, cancellationToken);
        if (collection is null || product is null)
        {
            return CatalogResult<bool>.NotFound();
        }

        if (product.Collections.All(link => link.CollectionId != collectionId))
        {
            var link = new ProductCollection
            {
                ProductId = product.Id,
                CollectionId = collection.Id,
                Product = product,
                Collection = collection,
            };
            product.Collections.Add(link);
            store.Add(link);
        }

        return await CatalogSupport.SaveProductAsync(store, product, expectedVersion, time.GetUtcNow(), () => true, cancellationToken);
    }

    public async Task<CatalogResult<bool>> RemoveProductAsync(
        Guid collectionId,
        Guid productId,
        CancellationToken cancellationToken,
        int? expectedVersion = null)
    {
        var product = await store.FindProductGraphAsync(productId, cancellationToken);
        if (product is null || await store.FindCollectionAsync(collectionId, cancellationToken) is null)
        {
            return CatalogResult<bool>.NotFound();
        }

        var link = product.Collections.FirstOrDefault(item => item.CollectionId == collectionId);
        if (link is not null)
        {
            product.Collections.Remove(link);
            store.Remove(link);
        }

        return await CatalogSupport.SaveProductAsync(store, product, expectedVersion, time.GetUtcNow(), () => true, cancellationToken);
    }
}
