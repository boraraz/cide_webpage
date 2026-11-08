namespace Commerce.Domain.Catalog;

public sealed class InventoryBalance
{
    public Guid VariantId { get; set; }

    public ProductVariant Variant { get; set; } = null!;

    public int OnHand { get; set; }

    public int Reserved { get; set; }

    public int Available => OnHand - Reserved;
}
