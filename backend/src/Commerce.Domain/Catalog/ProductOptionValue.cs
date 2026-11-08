namespace Commerce.Domain.Catalog;

public sealed class ProductOptionValue
{
    public Guid Id { get; set; }

    public Guid OptionId { get; set; }

    public ProductOption Option { get; set; } = null!;

    public string Value { get; set; } = string.Empty;

    public string NormalizedValue { get; set; } = string.Empty;

    public string? Code { get; set; }

    public int Position { get; set; }

    public ICollection<VariantOptionValue> VariantSelections { get; set; } = [];
}
