using Commerce.Domain.Catalog;

namespace Commerce.Application.Catalog;

public sealed class InventoryService(ICatalogStore store)
{
    public async Task<CatalogResult<MovementResponse>> AdjustAsync(
        Guid variantId,
        Guid actorUserId,
        string? idempotencyKey,
        string? movementType,
        int quantity,
        string? note,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (!CatalogSupport.IdempotencyKeyIsValid(idempotencyKey))
        {
            errors["idempotencyKey"] = ["Provide an idempotency key of 16 to 80 letters, numbers, hyphens or underscores."];
        }

        if (quantity <= 0)
        {
            errors["quantity"] = ["Enter a quantity greater than zero."];
        }

        if (!Enum.TryParse<InventoryMovementType>(movementType, true, out var type)
            || type is InventoryMovementType.InitialStock)
        {
            errors["movementType"] = ["Use manualIncrease, manualDecrease or correction."];
        }

        if (note is not null && note.Trim().Length > PublishRules.NoteMaxLength)
        {
            errors["note"] = [$"Use at most {PublishRules.NoteMaxLength} characters."];
        }

        if (errors.Count > 0)
        {
            return CatalogResult<MovementResponse>.Invalid(errors);
        }

        var outcome = await store.AdjustStockAsync(
            new StockAdjustment(
                variantId,
                actorUserId,
                idempotencyKey!.Trim(),
                type,
                quantity,
                string.IsNullOrWhiteSpace(note) ? null : note.Trim()),
            cancellationToken);

        return outcome switch
        {
            StockAdjustmentOutcome.Applied applied => CatalogResult<MovementResponse>.Ok(CatalogMapper.Movement(applied.Movement)),
            StockAdjustmentOutcome.Missing => CatalogResult<MovementResponse>.NotFound(),
            StockAdjustmentOutcome.Insufficient => CatalogResult<MovementResponse>.Conflict("quantity", "There is not enough stock."),
            StockAdjustmentOutcome.IdempotencyConflict => CatalogResult<MovementResponse>.Conflict(
                "idempotencyKey",
                "This idempotency key was already used with a different adjustment."),
            _ => throw new InvalidOperationException("Unexpected stock adjustment result."),
        };
    }
}
