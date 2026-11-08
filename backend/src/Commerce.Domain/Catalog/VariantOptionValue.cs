namespace Commerce.Domain.Catalog;

public sealed class VariantOptionValue
{
    public Guid VariantId { get; set; }

    public ProductVariant Variant { get; set; } = null!;

    public Guid OptionValueId { get; set; }

    public ProductOptionValue OptionValue { get; set; } = null!;
}
