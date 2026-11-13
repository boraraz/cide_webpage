using System.Diagnostics.Metrics;

namespace Commerce.Infrastructure.Ordering;

public static class CommerceMetrics
{
    public const string MeterName = "Commerce.Commerce";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> OrdersPlaced = Meter.CreateCounter<long>("commerce.orders.placed");
    private static readonly Counter<long> ReservationsReleased = Meter.CreateCounter<long>("commerce.reservations.released");
    private static readonly Counter<long> OutboxSent = Meter.CreateCounter<long>("commerce.outbox.sent");
    private static readonly Counter<long> OutboxFailed = Meter.CreateCounter<long>("commerce.outbox.failed");
    private static readonly Counter<long> MaintenanceFailed = Meter.CreateCounter<long>("commerce.maintenance.failed");

    public static void OrderPlaced() => OrdersPlaced.Add(1);

    public static void ReservationReleased() => ReservationsReleased.Add(1);

    public static void OutboxMessageSent() => OutboxSent.Add(1);

    public static void OutboxMessageFailed() => OutboxFailed.Add(1);

    public static void MaintenanceException() => MaintenanceFailed.Add(1);
}
