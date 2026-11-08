namespace Commerce.Domain.Catalog;

public static class PublishRules
{
    public const int NameMaxLength = 160;

    public const int ShortDescriptionMaxLength = 300;

    public const int DescriptionMaxLength = 8000;

    public const int BrandMaxLength = 80;

    public const int CategoryNameMaxLength = 120;

    public const int DescriptionOptionalMaxLength = 4000;

    public const int OptionNameMaxLength = 40;

    public const int AltTextMaxLength = 200;

    public const int NoteMaxLength = 500;

    public const int CodeMaxLength = 16;

    public static Dictionary<string, string[]> ForProduct(Product product)
    {
        var errors = new Dictionary<string, List<string>>();
        RequireText(errors, "name", product.Name, NameMaxLength);
        if (Slug.Normalize(product.Slug) != product.Slug)
        {
            Add(errors, "slug", "Enter a valid slug.");
        }

        RequireText(errors, "shortDescription", product.ShortDescription, ShortDescriptionMaxLength);
        RequireText(errors, "description", product.Description, DescriptionMaxLength);

        if (product.Brand is not null && product.Brand.Length > BrandMaxLength)
        {
            Add(errors, "brand", $"Use at most {BrandMaxLength} characters.");
        }

        if (product.Categories.Count == 0 || product.Categories.All(link => !link.Category.IsActive))
        {
            Add(errors, "categoryIds", "Choose at least one active category.");
        }

        var activeVariants = product.Variants.Where(variant => variant.IsActive).ToArray();
        if (activeVariants.Length == 0)
        {
            Add(errors, "variants", "Add at least one active variant.");
        }

        var optionIds = product.Options.Select(option => option.Id).ToHashSet();
        foreach (var variant in activeVariants)
        {
            if (variant.Price <= 0m)
            {
                Add(errors, "price", "Every active variant needs a price greater than zero.");
            }

            if (!Money.CompareAtIsValid(variant.Price, variant.CompareAtPrice))
            {
                Add(errors, "compareAtPrice", "The compare-at price must be greater than the price.");
            }

            if (Sku.Normalize(variant.Sku) != variant.Sku)
            {
                Add(errors, "sku", "Every active variant needs a valid SKU.");
            }

            if (!CombinationIsComplete(product, variant, optionIds))
            {
                Add(errors, "optionValueIds", "Each active variant must choose one value for every option.");
            }
        }

        if (product.Media.Count == 0 || product.Media.All(media => string.IsNullOrWhiteSpace(media.AltText)))
        {
            Add(errors, "media", "Add an image with alt text before publishing.");
        }
        else if (product.Media.Count(media => media.IsPrimary) != 1)
        {
            Add(errors, "media", "Choose one primary image.");
        }

        return errors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());
    }

    public static bool CombinationIsComplete(Product product, ProductVariant variant, HashSet<Guid>? optionIds = null)
    {
        optionIds ??= product.Options.Select(option => option.Id).ToHashSet();
        if (optionIds.Count == 0)
        {
            return variant.CombinationKey.Length == 0 && variant.Selections.Count == 0;
        }

        var selected = variant.Selections.Select(selection => selection.OptionValue.OptionId).ToHashSet();
        return selected.SetEquals(optionIds) && variant.Selections.Count == optionIds.Count;
    }

    private static void RequireText(Dictionary<string, List<string>> errors, string field, string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Add(errors, field, "This field is required.");
        }
        else if (value.Trim().Length > max)
        {
            Add(errors, field, $"Use at most {max} characters.");
        }
    }

    private static void Add(Dictionary<string, List<string>> errors, string key, string message)
    {
        if (!errors.TryGetValue(key, out var list))
        {
            list = [];
            errors[key] = list;
        }

        if (!list.Contains(message))
        {
            list.Add(message);
        }
    }
}
