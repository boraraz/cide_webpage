namespace Commerce.Domain.Catalog;

public sealed class ProductVariant
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    public Product Product { get; set; } = null!;

    public string Sku { get; set; } = string.Empty;

    public string? Barcode { get; set; }

    public decimal Price { get; set; }

    public decimal? CompareAtPrice { get; set; }

    public string CurrencyCode { get; set; } = Money.Currency;

    public bool IsActive { get; set; } = true;

    public string CombinationKey { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public InventoryBalance? Balance { get; set; }

    public ICollection<VariantOptionValue> Selections { get; set; } = [];
}
