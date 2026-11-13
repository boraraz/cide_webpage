namespace Commerce.Domain.Commerce;

public enum OrderStatus
{
    PaymentPending,
    Cancelled,
    Expired,
    Paid,
}

public enum OrderAddressKind
{
    Shipping,
    Billing,
}

public enum OutboxMessageType
{
    Received,
    Cancelled,
    Expired,
}

public sealed class Cart
{
    public Guid Id { get; set; }

    public Guid? CustomerId { get; set; }

    public byte[]? TokenHash { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public Guid? MergedIntoCartId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<CartItem> Items { get; set; } = [];
}

public sealed class CartItem
{
    public Guid Id { get; set; }

    public Guid CartId { get; set; }

    public Cart Cart { get; set; } = null!;

    public Guid VariantId { get; set; }

    public int Quantity { get; set; }

    public bool QuantityLimited { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class CustomerOrder
{
    public Guid Id { get; set; }

    public string Reference { get; set; } = string.Empty;

    public Guid? CustomerId { get; set; }

    public string Email { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public OrderStatus Status { get; set; }

    public string Currency { get; set; } = Catalog.Money.Currency;

    public decimal MerchandiseTotal { get; set; }

    public decimal ShippingTotal { get; set; }

    public decimal GrandTotal { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<CustomerOrderItem> Items { get; set; } = [];

    public ICollection<CustomerOrderAddress> Addresses { get; set; } = [];

    public ICollection<OrderStatusChange> History { get; set; } = [];
}

public sealed class CustomerOrderItem
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public CustomerOrder Order { get; set; } = null!;

    public Guid VariantId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public string Sku { get; set; } = string.Empty;

    public string OptionsJson { get; set; } = "[]";

    public decimal UnitPrice { get; set; }

    public int Quantity { get; set; }

    public decimal LineTotal { get; set; }
}

public sealed class CustomerOrderAddress
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public CustomerOrder Order { get; set; } = null!;

    public OrderAddressKind Kind { get; set; }

    public string Recipient { get; set; } = string.Empty;

    public string Line1 { get; set; } = string.Empty;

    public string? Line2 { get; set; }

    public string District { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string PostalCode { get; set; } = string.Empty;

    public string Country { get; set; } = "TR";

    public string? Notes { get; set; }
}

public sealed class OrderStatusChange
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public CustomerOrder Order { get; set; } = null!;

    public OrderStatus? FromStatus { get; set; }

    public OrderStatus ToStatus { get; set; }

    public string Actor { get; set; } = string.Empty;

    public string? Reason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class OrderSubmission
{
    public Guid Id { get; set; }

    public string Scope { get; set; } = string.Empty;

    public string IdempotencyKey { get; set; } = string.Empty;

    public string Fingerprint { get; set; } = string.Empty;

    public Guid? OrderId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class OrderAccessToken
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public byte[] TokenHash { get; set; } = [];

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class OrderConfirmationSession
{
    public Guid Id { get; set; }

    public byte[] TokenHash { get; set; } = [];

    public byte[] GuestCartHash { get; set; } = [];

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<OrderConfirmationSessionOrder> Orders { get; set; } = [];
}

public sealed class OrderConfirmationSessionOrder
{
    public Guid SessionId { get; set; }

    public OrderConfirmationSession Session { get; set; } = null!;

    public Guid OrderId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class EmailOutbox
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public OutboxMessageType MessageType { get; set; }

    public string Payload { get; set; } = string.Empty;

    public int Attempts { get; set; }

    public DateTimeOffset NextAttemptAt { get; set; }

    public DateTimeOffset? LockedUntil { get; set; }

    public string? LockedBy { get; set; }

    public DateTimeOffset? SentAt { get; set; }

    public string? LastError { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
