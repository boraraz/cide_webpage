namespace Commerce.Application.Commerce;

public sealed class CommerceOptions
{
    public const string SectionName = "Commerce";

    public decimal? ShippingFee { get; set; }

    public decimal? FreeShippingThreshold { get; set; }

    public int PendingPaymentMinutes { get; set; } = 30;

    public int MaxQuantityPerLine { get; set; } = 10;

    public int CartLifetimeDays { get; set; } = 30;

    public int OrderAccessDays { get; set; } = 30;

    public int CartWritePermitLimit { get; set; } = 120;

    public int QuotePermitLimit { get; set; } = 60;

    public int SubmitPermitLimit { get; set; } = 30;

    public int GuestLookupPermitLimit { get; set; } = 30;
}

public interface ICommerceStore
{
    Task<CartModel> GetCartAsync(Guid? customerId, byte[]? guestHash, CancellationToken cancellationToken);

    Task<CommerceResult<CartChange>> AddItemAsync(
        Guid? customerId,
        byte[]? guestHash,
        Guid variantId,
        int quantity,
        CancellationToken cancellationToken);

    Task<CommerceResult<CartModel>> UpdateItemAsync(
        Guid? customerId,
        byte[]? guestHash,
        Guid itemId,
        int quantity,
        CancellationToken cancellationToken);

    Task<CommerceResult<CartModel>> RemoveItemAsync(
        Guid? customerId,
        byte[]? guestHash,
        Guid itemId,
        CancellationToken cancellationToken);

    Task MergeAsync(byte[]? guestHash, Guid customerId, CancellationToken cancellationToken);

    Task<CommerceResult<QuoteModel>> QuoteAsync(Guid? customerId, byte[]? guestHash, CancellationToken cancellationToken);

    Task<CommerceResult<PlacedOrderModel>> SubmitAsync(
        Guid? customerId,
        byte[]? guestHash,
        SubmitCommand command,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<OrderModel>> ListForCustomerAsync(Guid customerId, CancellationToken cancellationToken);

    Task<OrderModel?> FindForCustomerAsync(Guid customerId, string reference, CancellationToken cancellationToken);

    Task<OrderModel?> FindByAccessTokenAsync(string rawToken, CancellationToken cancellationToken);

    Task<CommerceResult<AdminOrderPageModel>> ListAdminAsync(
        string? status,
        string? reference,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<OrderModel?> FindAdminAsync(Guid orderId, CancellationToken cancellationToken);

    Task<CommerceResult<OrderModel>> CancelAsync(Guid orderId, Guid actorId, string? reason, CancellationToken cancellationToken);
}

public interface ICommerceMaintenance
{
    Task<int> ExpireDueAsync(CancellationToken cancellationToken);

    Task<int> DispatchOutboxAsync(CancellationToken cancellationToken);
}
