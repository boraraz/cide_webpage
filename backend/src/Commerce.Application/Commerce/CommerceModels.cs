using System.Security.Cryptography;
using System.Text;
using Commerce.Domain.Catalog;

namespace Commerce.Application.Commerce;

public sealed record OptionPair(string Name, string Value);

public sealed record PostalAddress(
    string Recipient,
    string Line1,
    string? Line2,
    string District,
    string City,
    string PostalCode,
    string Country,
    string? Notes);

public sealed record LineIntent(Guid VariantId, int Quantity);

public sealed record CartLineModel(
    Guid Id,
    Guid VariantId,
    string ProductName,
    string Sku,
    IReadOnlyList<OptionPair> Options,
    string UnitPrice,
    int Quantity,
    string LineTotal,
    int AvailableQuantity,
    bool Purchasable,
    string? UnavailableReason,
    bool QuantityLimited);

public sealed record CartModel(
    Guid? Id,
    int ItemCount,
    string Currency,
    IReadOnlyList<CartLineModel> Items,
    DateTimeOffset? ExpiresAt);

public sealed record CartChange(CartModel Cart, string? IssuedToken, DateTimeOffset? TokenExpiresAt);

public sealed record QuoteLineModel(
    Guid VariantId,
    string ProductName,
    string Sku,
    IReadOnlyList<OptionPair> Options,
    string UnitPrice,
    int Quantity,
    string LineTotal,
    bool Purchasable,
    string? UnavailableReason);

public sealed record QuoteModel(
    string Currency,
    string MerchandiseTotal,
    string ShippingTotal,
    string GrandTotal,
    int ReservationExpiresInMinutes,
    string ShippingFee,
    string FreeShippingThreshold,
    bool Purchasable,
    IReadOnlyList<QuoteLineModel> Lines);

public sealed record OrderLineModel(
    Guid VariantId,
    string ProductName,
    string Sku,
    IReadOnlyList<OptionPair> Options,
    string UnitPrice,
    int Quantity,
    string LineTotal);

public sealed record OrderAddressModel(
    string Kind,
    string Recipient,
    string Line1,
    string? Line2,
    string District,
    string City,
    string PostalCode,
    string Country,
    string? Notes);

public sealed record OrderHistoryModel(string? FromStatus, string ToStatus, string Actor, string? Reason, DateTimeOffset CreatedAt);

public sealed record OrderModel(
    Guid Id,
    string Reference,
    string Status,
    string Email,
    string Phone,
    string Currency,
    string MerchandiseTotal,
    string ShippingTotal,
    string GrandTotal,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt,
    bool PaymentCollected,
    int ReservationMinutes,
    IReadOnlyList<OrderLineModel> Items,
    IReadOnlyList<OrderAddressModel> Addresses,
    IReadOnlyList<OrderHistoryModel> History);

public sealed record PlacedOrderModel(OrderModel Order, string AccessToken);

public sealed record SubmitCommand(
    string Fingerprint,
    string IdempotencyKey,
    string Email,
    string Phone,
    IReadOnlyList<LineIntent> Lines,
    PostalAddress Shipping,
    PostalAddress Billing,
    decimal ExpectedGrandTotal);

public sealed record AdminOrderRowModel(
    Guid Id,
    string Reference,
    string Status,
    string Email,
    string GrandTotal,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt);

public sealed record AdminOrderPageModel(
    IReadOnlyList<AdminOrderRowModel> Items,
    int Page,
    int PageSize,
    int Total,
    string ShippingFee,
    string FreeShippingThreshold,
    int PendingPaymentMinutes);

public sealed record CommerceFailure(
    int Status,
    string Title,
    string? Detail = null,
    IReadOnlyDictionary<string, string[]>? Errors = null);

public sealed record CommerceResult<T>(T? Value, CommerceFailure? Failure)
{
    public bool Succeeded => Failure is null;

    public static CommerceResult<T> Ok(T value) => new(value, null);

    public static CommerceResult<T> Fail(CommerceFailure failure) => new(default, failure);

    public static CommerceResult<T> Invalid(IReadOnlyDictionary<string, string[]> errors) =>
        Fail(new CommerceFailure(400, "One or more validation errors occurred.", Errors: errors));
}

public static class CheckoutFingerprint
{
    public static string Compute(
        IReadOnlyList<LineIntent> lines,
        string email,
        string phone,
        PostalAddress shipping,
        PostalAddress billing)
    {
        var ordered = lines
            .OrderBy(line => line.VariantId)
            .Select(line => $"{line.VariantId:N}:{line.Quantity}");
        var payload = string.Join(
            '\n',
            [
                "v1",
                string.Join(',', ordered),
                email.Trim().ToLowerInvariant(),
                phone,
                Canon(shipping),
                Canon(billing),
            ]);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    private static string Canon(PostalAddress address) =>
        string.Join(
            '\u001f',
            [
                address.Recipient.Trim(),
                address.Line1.Trim(),
                address.Line2?.Trim() ?? string.Empty,
                address.District.Trim(),
                address.City.Trim(),
                address.PostalCode.Trim(),
                address.Country.Trim(),
                address.Notes?.Trim() ?? string.Empty,
            ]);
}

public static class CommerceMoney
{
    public static string Format(decimal amount) => Money.Format(amount);
}
