namespace Commerce.Domain.Catalog;

public sealed class InventoryMovement
{
    public Guid Id { get; set; }

    public Guid VariantId { get; set; }

    public ProductVariant Variant { get; set; } = null!;

    public InventoryMovementType MovementType { get; set; }

    public int QuantityDelta { get; set; }

    public int ReservedDelta { get; set; }

    public int OnHandAfter { get; set; }

    public int ReservedAfter { get; set; }

    public string? Note { get; set; }

    public Guid? ActorUserId { get; set; }

    public string IdempotencyKey { get; set; } = string.Empty;

    public string RequestHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}
