using Commerce.Domain.Catalog;
using Commerce.Domain.Commerce;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Commerce.Infrastructure.Persistence.Configurations;

internal sealed class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> builder)
    {
        builder.ToTable("carts");
        builder.HasKey(cart => cart.Id);
        builder.Property(cart => cart.TokenHash).HasMaxLength(32);
        builder.HasIndex(cart => cart.CustomerId).IsUnique();
        builder.HasIndex(cart => cart.TokenHash).IsUnique();
        builder.HasOne<Cart>()
            .WithMany()
            .HasForeignKey(cart => cart.MergedIntoCartId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> builder)
    {
        builder.ToTable("cart_items");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.CartId, item.VariantId }).IsUnique();
        builder.ToTable(table => table.HasCheckConstraint("ck_cart_items_quantity_positive", "quantity > 0"));
        builder.HasOne(item => item.Cart)
            .WithMany(cart => cart.Items)
            .HasForeignKey(item => item.CartId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class CustomerOrderConfiguration : IEntityTypeConfiguration<CustomerOrder>
{
    public void Configure(EntityTypeBuilder<CustomerOrder> builder)
    {
        builder.ToTable("orders");
        builder.HasKey(order => order.Id);
        builder.Property(order => order.Reference).HasMaxLength(16).IsRequired();
        builder.HasIndex(order => order.Reference).IsUnique();
        builder.Property(order => order.Email).HasMaxLength(254).IsRequired();
        builder.Property(order => order.Phone).HasMaxLength(16).IsRequired();
        builder.Property(order => order.Currency).HasMaxLength(3).IsRequired();
        builder.Property(order => order.Status)
            .HasConversion(status => status.ToString().ToLowerInvariant(), value => Enum.Parse<OrderStatus>(value, true))
            .HasMaxLength(32);
        builder.Property(order => order.MerchandiseTotal).HasPrecision(12, 2);
        builder.Property(order => order.ShippingTotal).HasPrecision(12, 2);
        builder.Property(order => order.GrandTotal).HasPrecision(12, 2);
        builder.HasIndex(order => new { order.Status, order.ExpiresAt });
        builder.HasIndex(order => new { order.CustomerId, order.CreatedAt });
    }
}

internal sealed class CustomerOrderItemConfiguration : IEntityTypeConfiguration<CustomerOrderItem>
{
    public void Configure(EntityTypeBuilder<CustomerOrderItem> builder)
    {
        builder.ToTable("order_items");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.ProductName).HasMaxLength(160).IsRequired();
        builder.Property(item => item.Sku).HasMaxLength(64).IsRequired();
        builder.Property(item => item.OptionsJson).IsRequired();
        builder.Property(item => item.UnitPrice).HasPrecision(12, 2);
        builder.Property(item => item.LineTotal).HasPrecision(12, 2);
        builder.ToTable(table => table.HasCheckConstraint("ck_order_items_quantity_positive", "quantity > 0"));
        builder.HasOne(item => item.Order)
            .WithMany(order => order.Items)
            .HasForeignKey(item => item.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ProductVariant>()
            .WithMany()
            .HasForeignKey(item => item.VariantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CustomerOrderAddressConfiguration : IEntityTypeConfiguration<CustomerOrderAddress>
{
    public void Configure(EntityTypeBuilder<CustomerOrderAddress> builder)
    {
        builder.ToTable("order_addresses");
        builder.HasKey(address => address.Id);
        builder.Property(address => address.Kind)
            .HasConversion(kind => kind.ToString().ToLowerInvariant(), value => Enum.Parse<OrderAddressKind>(value, true))
            .HasMaxLength(16);
        builder.Property(address => address.Recipient).HasMaxLength(120).IsRequired();
        builder.Property(address => address.Line1).HasMaxLength(200).IsRequired();
        builder.Property(address => address.Line2).HasMaxLength(200);
        builder.Property(address => address.District).HasMaxLength(120).IsRequired();
        builder.Property(address => address.City).HasMaxLength(120).IsRequired();
        builder.Property(address => address.PostalCode).HasMaxLength(5).IsRequired();
        builder.Property(address => address.Country).HasMaxLength(2).IsRequired();
        builder.Property(address => address.Notes).HasMaxLength(500);
        builder.HasIndex(address => new { address.OrderId, address.Kind }).IsUnique();
        builder.HasOne(address => address.Order)
            .WithMany(order => order.Addresses)
            .HasForeignKey(address => address.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class OrderStatusChangeConfiguration : IEntityTypeConfiguration<OrderStatusChange>
{
    public void Configure(EntityTypeBuilder<OrderStatusChange> builder)
    {
        builder.ToTable("order_status_history");
        builder.HasKey(change => change.Id);
        builder.Property(change => change.FromStatus)
            .HasConversion(
                status => status == null ? null : status.Value.ToString().ToLowerInvariant(),
                value => value == null ? null : Enum.Parse<OrderStatus>(value, true))
            .HasMaxLength(32);
        builder.Property(change => change.ToStatus)
            .HasConversion(status => status.ToString().ToLowerInvariant(), value => Enum.Parse<OrderStatus>(value, true))
            .HasMaxLength(32);
        builder.Property(change => change.Actor).HasMaxLength(80).IsRequired();
        builder.Property(change => change.Reason).HasMaxLength(500);
        builder.HasOne(change => change.Order)
            .WithMany(order => order.History)
            .HasForeignKey(change => change.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class OrderSubmissionConfiguration : IEntityTypeConfiguration<OrderSubmission>
{
    public void Configure(EntityTypeBuilder<OrderSubmission> builder)
    {
        builder.ToTable("order_submissions");
        builder.HasKey(submission => submission.Id);
        builder.Property(submission => submission.Scope).HasMaxLength(80).IsRequired();
        builder.Property(submission => submission.IdempotencyKey).HasMaxLength(80).IsRequired();
        builder.Property(submission => submission.Fingerprint).HasMaxLength(64).IsRequired();
        builder.HasIndex(submission => new { submission.Scope, submission.IdempotencyKey }).IsUnique();
        builder.HasIndex(submission => submission.CreatedAt);
    }
}

internal sealed class OrderAccessTokenConfiguration : IEntityTypeConfiguration<OrderAccessToken>
{
    public void Configure(EntityTypeBuilder<OrderAccessToken> builder)
    {
        builder.ToTable("order_access_tokens");
        builder.HasKey(token => token.Id);
        builder.Property(token => token.TokenHash).HasMaxLength(32).IsRequired();
        builder.HasIndex(token => token.TokenHash).IsUnique();
        builder.HasIndex(token => token.OrderId);
    }
}

internal sealed class OrderConfirmationSessionConfiguration : IEntityTypeConfiguration<OrderConfirmationSession>
{
    public void Configure(EntityTypeBuilder<OrderConfirmationSession> builder)
    {
        builder.ToTable("order_confirmation_sessions");
        builder.HasKey(session => session.Id);
        builder.Property(session => session.TokenHash).HasMaxLength(32).IsRequired();
        builder.Property(session => session.GuestCartHash).HasMaxLength(32).IsRequired();
        builder.HasIndex(session => session.TokenHash).IsUnique();
    }
}

internal sealed class OrderConfirmationSessionOrderConfiguration : IEntityTypeConfiguration<OrderConfirmationSessionOrder>
{
    public void Configure(EntityTypeBuilder<OrderConfirmationSessionOrder> builder)
    {
        builder.ToTable("order_confirmation_session_orders");
        builder.HasKey(link => new { link.SessionId, link.OrderId });
        builder.HasOne(link => link.Session)
            .WithMany(session => session.Orders)
            .HasForeignKey(link => link.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<CustomerOrder>()
            .WithMany()
            .HasForeignKey(link => link.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class EmailOutboxConfiguration : IEntityTypeConfiguration<EmailOutbox>
{
    public void Configure(EntityTypeBuilder<EmailOutbox> builder)
    {
        builder.ToTable("email_outbox");
        builder.HasKey(message => message.Id);
        builder.Property(message => message.MessageType)
            .HasConversion(type => type.ToString().ToLowerInvariant(), value => Enum.Parse<OutboxMessageType>(value, true))
            .HasMaxLength(32);
        builder.Property(message => message.Payload).IsRequired();
        builder.Property(message => message.LockedBy).HasMaxLength(80);
        builder.Property(message => message.LastError).HasMaxLength(40);
        builder.HasIndex(message => new { message.OrderId, message.MessageType }).IsUnique();
        builder.HasIndex(message => new { message.SentAt, message.NextAttemptAt });
    }
}
