using Commerce.Domain.Catalog;

namespace Commerce.Application.Catalog;

public static class CatalogMapper
{
    public static CategoryResponse Category(Category category) => new(
        category.Id,
        category.Name,
        category.Slug,
        category.Description,
        category.ParentCategoryId,
        category.DisplayOrder,
        category.IsActive,
        category.CreatedAt,
        category.UpdatedAt);

    public static CollectionResponse Collection(Collection collection) => new(
        collection.Id,
        collection.Name,
        collection.Slug,
        collection.Description,
        collection.DisplayOrder,
        collection.IsActive,
        collection.CreatedAt,
        collection.UpdatedAt);

    public static ProductResponse Product(Product product, bool includeStock) => new(
        product.Id,
        product.Name,
        product.Slug,
        product.ShortDescription,
        product.Description,
        product.Brand,
        product.IsFeatured,
        product.Status.ToString().ToLowerInvariant(),
        product.PublishedAt,
        product.CreatedAt,
        product.UpdatedAt,
        product.Categories
            .Select(link => new NamedSlug(link.Category.Id, link.Category.Name, link.Category.Slug))
            .OrderBy(link => link.Name)
            .ToArray(),
        product.Collections
            .Select(link => new NamedSlug(link.Collection.Id, link.Collection.Name, link.Collection.Slug))
            .OrderBy(link => link.Name)
            .ToArray(),
        product.Options.OrderBy(option => option.Position).ThenBy(option => option.Name).Select(Option).ToArray(),
        product.Variants.OrderBy(variant => variant.Sku).Select(variant => Variant(variant, includeStock)).ToArray(),
        product.Media.OrderBy(media => media.DisplayOrder).ThenBy(media => media.CreatedAt).Select(Media).ToArray());

    public static MovementResponse Movement(InventoryMovement movement) => new(
        movement.Id,
        movement.VariantId,
        movement.MovementType.ToString().ToLowerInvariant(),
        movement.QuantityDelta,
        movement.OnHandAfter,
        movement.ReservedAfter,
        movement.Note,
        movement.ActorUserId,
        movement.IdempotencyKey,
        movement.CreatedAt);

    public static string MediaPath(Guid id) => $"/api/v1/media/{id}";

    private static OptionResponse Option(ProductOption option) => new(
        option.Id,
        option.Name,
        option.Position,
        option.Values
            .OrderBy(value => value.Position)
            .ThenBy(value => value.Value)
            .Select(value => new OptionValueResponse(value.Id, value.Value, value.Code, value.Position))
            .ToArray());

    private static VariantResponse Variant(ProductVariant variant, bool includeStock)
    {
        var balance = variant.Balance;
        return new VariantResponse(
            variant.Id,
            variant.Sku,
            variant.Barcode,
            variant.IsActive,
            Money.Format(variant.Price),
            variant.CompareAtPrice is decimal compareAt ? Money.Format(compareAt) : null,
            variant.CurrencyCode,
            includeStock ? balance?.OnHand ?? 0 : 0,
            includeStock ? balance?.Reserved ?? 0 : 0,
            includeStock ? balance?.Available ?? 0 : 0,
            Selections(variant));
    }

    private static IReadOnlyList<SelectedOptionResponse> Selections(ProductVariant variant) =>
        variant.Selections
            .OrderBy(selection => selection.OptionValue.Option.Position)
            .Select(selection => new SelectedOptionResponse(
                selection.OptionValue.Option.Name,
                selection.OptionValue.Value,
                selection.OptionValue.Code))
            .ToArray();

    private static MediaResponse Media(ProductMedia media) => new(
        media.Id,
        media.VariantId,
        media.AltText,
        media.DisplayOrder,
        media.IsPrimary,
        media.ContentType,
        media.FileSize,
        MediaPath(media.Id));
}
