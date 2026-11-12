namespace Commerce.Domain.Catalog;

public enum InventoryMovementType
{
    InitialStock,
    ManualIncrease,
    ManualDecrease,
    Correction,
    Reservation,
    ReservationRelease,
}
