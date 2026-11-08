using Commerce.Domain.Catalog;

namespace Commerce.Application.Catalog;

public sealed class CategoryService(ICatalogStore store, TimeProvider time)
{
    public async Task<CatalogResult<CategoryResponse>> CreateAsync(
        string? name,
        string? slug,
        string? description,
        Guid? parentCategoryId,
        int displayOrder,
        bool isActive,
        CancellationToken cancellationToken)
    {
        var errors = CatalogSupport.Text("name", name, PublishRules.CategoryNameMaxLength, required: true);
        Merge(errors, CatalogSupport.Text("description", description, PublishRules.DescriptionOptionalMaxLength, required: false));
        if (errors.Count > 0)
        {
            return CatalogResult<CategoryResponse>.Invalid(errors);
        }

        if (parentCategoryId is Guid parentId && await store.FindCategoryAsync(parentId, cancellationToken) is null)
        {
            return CatalogResult<CategoryResponse>.NotFound();
        }

        var resolved = await CatalogSupport.ResolveSlugAsync(store, SlugOwner.Category, slug, name!, null, cancellationToken);
        if (resolved is null)
        {
            return CatalogResult<CategoryResponse>.Invalid(new Dictionary<string, string[]> { ["slug"] = ["Enter a valid slug."] });
        }

        if (resolved.Length == 0)
        {
            return CatalogResult<CategoryResponse>.Conflict("slug", "That slug is already in use.");
        }

        var now = time.GetUtcNow();
        var category = new Category
        {
            Id = Guid.CreateVersion7(),
            Name = name!.Trim(),
            Slug = resolved,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            ParentCategoryId = parentCategoryId,
            DisplayOrder = displayOrder,
            IsActive = isActive,
            CreatedAt = now,
            UpdatedAt = now,
        };
        store.Add(category);
        return await CatalogSupport.SaveAsync(store, () => CatalogMapper.Category(category), cancellationToken);
    }

    public async Task<CatalogResult<CategoryResponse>> UpdateAsync(
        Guid id,
        string? name,
        string? slug,
        string? description,
        Guid? parentCategoryId,
        bool parentSpecified,
        int? displayOrder,
        bool? isActive,
        CancellationToken cancellationToken)
    {
        var category = await store.FindCategoryAsync(id, cancellationToken);
        if (category is null)
        {
            return CatalogResult<CategoryResponse>.NotFound();
        }

        var errors = new Dictionary<string, string[]>();
        if (name is not null)
        {
            Merge(errors, CatalogSupport.Text("name", name, PublishRules.CategoryNameMaxLength, required: true));
        }

        if (description is not null)
        {
            Merge(errors, CatalogSupport.Text("description", description, PublishRules.DescriptionOptionalMaxLength, required: false));
        }

        if (errors.Count > 0)
        {
            return CatalogResult<CategoryResponse>.Invalid(errors);
        }

        if (parentSpecified && parentCategoryId is Guid parentId)
        {
            if (await store.FindCategoryAsync(parentId, cancellationToken) is null)
            {
                return CatalogResult<CategoryResponse>.NotFound();
            }

            if (await store.ParentCreatesCycleAsync(category.Id, parentId, cancellationToken))
            {
                return CatalogResult<CategoryResponse>.Conflict("parentCategoryId", "A category cannot be placed inside itself.");
            }
        }

        if (slug is not null || name is not null)
        {
            var resolved = await CatalogSupport.ResolveSlugAsync(
                store,
                SlugOwner.Category,
                slug ?? category.Slug,
                name ?? category.Name,
                category.Id,
                cancellationToken);
            if (resolved is null)
            {
                return CatalogResult<CategoryResponse>.Invalid(new Dictionary<string, string[]> { ["slug"] = ["Enter a valid slug."] });
            }

            if (resolved.Length == 0)
            {
                return CatalogResult<CategoryResponse>.Conflict("slug", "That slug is already in use.");
            }

            store.RetireSlug(SlugOwner.Category, category.Id, category.Slug, resolved, time.GetUtcNow());
            category.Slug = resolved;
        }

        if (name is not null)
        {
            category.Name = name.Trim();
        }

        if (description is not null)
        {
            category.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        }

        if (parentSpecified)
        {
            category.ParentCategoryId = parentCategoryId;
        }

        if (displayOrder is int order)
        {
            category.DisplayOrder = order;
        }

        if (isActive is bool active)
        {
            category.IsActive = active;
        }

        category.UpdatedAt = time.GetUtcNow();
        return await CatalogSupport.SaveAsync(store, () => CatalogMapper.Category(category), cancellationToken);
    }

    public async Task<CatalogResult<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var category = await store.FindCategoryAsync(id, cancellationToken);
        if (category is null)
        {
            return CatalogResult<bool>.NotFound();
        }

        if (await store.CategoryHasChildrenAsync(id, cancellationToken) || await store.CategoryHasProductsAsync(id, cancellationToken))
        {
            return CatalogResult<bool>.Conflict(
                "category",
                "Remove child categories and product assignments before deleting this category.");
        }

        store.Remove(category);
        return await CatalogSupport.SaveAsync(store, () => true, cancellationToken);
    }

    private static void Merge(Dictionary<string, string[]> target, Dictionary<string, string[]> source)
    {
        foreach (var (key, value) in source)
        {
            target[key] = value;
        }
    }
}
