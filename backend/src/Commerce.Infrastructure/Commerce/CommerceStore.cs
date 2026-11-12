using System.Text.Json;
using Commerce.Application.Commerce;
using Commerce.Application.Identity;
using Commerce.Domain.Catalog;
using Commerce.Domain.Commerce;
using Commerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Commerce.Infrastructure.Ordering;

public sealed partial class CommerceStore(
    CommerceDbContext database,
    TimeProvider time,
    IOptions<CommerceOptions> options,
    IEmailSender email) : ICommerceStore, ICommerceMaintenance
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CartModel> GetCartAsync(Guid? customerId, byte[]? guestHash, CancellationToken cancellationToken)
    {
        var cart = await FindCartAsync(customerId, guestHash, tracking: false, cancellationToken);
        if (cart is null || cart.ExpiresAt <= time.GetUtcNow())
        {
            return EmptyCart();
        }

        var lines = await PriceAsync(cart.Items, cancellationToken);
        return MapCart(cart.Id, cart.ExpiresAt, lines);
    }

    public async Task<CommerceResult<CartChange>> AddItemAsync(
        Guid? customerId,
        byte[]? guestHash,
        Guid variantId,
        int quantity,
        CancellationToken cancellationToken)
    {
        if (!await database.ProductVariants.AnyAsync(variant => variant.Id == variantId, cancellationToken))
        {
            return CommerceResult<CartChange>.Fail(new CommerceFailure(404, "That product option was not found."));
        }

        var max = MaxQuantity();
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var now = time.GetUtcNow();
        string? issued = null;
        var cart = await FindCartAsync(customerId, guestHash, tracking: true, cancellationToken);
        if (cart is null || cart.ExpiresAt <= now)
        {
            if (cart is not null)
            {
                database.CartItems.RemoveRange(cart.Items);
                cart.Items.Clear();
            }
            else
            {
                cart = NewCart(customerId, now);
                if (customerId is null)
                {
                    issued = SecureTokens.Create();
                    cart.TokenHash = SecureTokens.Hash(issued);
                }

                database.Carts.Add(cart);
            }
        }

        var item = cart.Items.FirstOrDefault(line => line.VariantId == variantId);
        var combined = (item?.Quantity ?? 0) + quantity;
        var limited = combined > max;
        var stored = Math.Min(combined, max);
        if (item is null)
        {
            item = new CartItem
            {
                Id = Guid.CreateVersion7(),
                CartId = cart.Id,
                VariantId = variantId,
                Quantity = stored,
                QuantityLimited = limited,
                CreatedAt = now,
                UpdatedAt = now,
            };
            database.CartItems.Add(item);
        }
        else
        {
            item.Quantity = stored;
            item.QuantityLimited = limited;
            item.UpdatedAt = now;
        }

        cart.ExpiresAt = now.AddDays(Math.Max(1, options.Value.CartLifetimeDays));
        cart.UpdatedAt = now;
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var lines = await PriceAsync(cart.Items, cancellationToken);
        return CommerceResult<CartChange>.Ok(new CartChange(MapCart(cart.Id, cart.ExpiresAt, lines), issued, cart.ExpiresAt));
    }

    public async Task<CommerceResult<CartModel>> UpdateItemAsync(
        Guid? customerId,
        byte[]? guestHash,
        Guid itemId,
        int quantity,
        CancellationToken cancellationToken)
    {
        var max = MaxQuantity();
        if (quantity > max)
        {
            return CommerceResult<CartModel>.Fail(new CommerceFailure(
                400,
                "One or more validation errors occurred.",
                Errors: new Dictionary<string, string[]> { ["quantity"] = [$"Enter a quantity from 1 to {max}."] }));
        }

        var cart = await FindCartAsync(customerId, guestHash, tracking: true, cancellationToken);
        var item = cart?.Items.FirstOrDefault(line => line.Id == itemId);
        if (cart is null || item is null || cart.ExpiresAt <= time.GetUtcNow())
        {
            return CommerceResult<CartModel>.Fail(new CommerceFailure(404, "That bag item was not found."));
        }

        var now = time.GetUtcNow();
        item.Quantity = quantity;
        item.QuantityLimited = false;
        item.UpdatedAt = now;
        cart.ExpiresAt = now.AddDays(Math.Max(1, options.Value.CartLifetimeDays));
        cart.UpdatedAt = now;
        await database.SaveChangesAsync(cancellationToken);
        var lines = await PriceAsync(cart.Items, cancellationToken);
        return CommerceResult<CartModel>.Ok(MapCart(cart.Id, cart.ExpiresAt, lines));
    }

    public async Task<CommerceResult<CartModel>> RemoveItemAsync(
        Guid? customerId,
        byte[]? guestHash,
        Guid itemId,
        CancellationToken cancellationToken)
    {
        var cart = await FindCartAsync(customerId, guestHash, tracking: true, cancellationToken);
        var item = cart?.Items.FirstOrDefault(line => line.Id == itemId);
        if (cart is null || item is null)
        {
            return CommerceResult<CartModel>.Fail(new CommerceFailure(404, "That bag item was not found."));
        }

        database.CartItems.Remove(item);
        cart.Items.Remove(item);
        var now = time.GetUtcNow();
        cart.ExpiresAt = now.AddDays(Math.Max(1, options.Value.CartLifetimeDays));
        cart.UpdatedAt = now;
        await database.SaveChangesAsync(cancellationToken);
        var lines = await PriceAsync(cart.Items, cancellationToken);
        return CommerceResult<CartModel>.Ok(MapCart(cart.Id, cart.ExpiresAt, lines));
    }

    public async Task MergeAsync(byte[]? guestHash, Guid customerId, CancellationToken cancellationToken)
    {
        if (guestHash is null)
        {
            return;
        }

        database.ChangeTracker.Clear();
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var guest = await database.Carts.Include(cart => cart.Items)
            .FirstOrDefaultAsync(cart => cart.TokenHash == guestHash, cancellationToken);
        if (guest is null || guest.MergedIntoCartId is not null || guest.CustomerId is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        var now = time.GetUtcNow();
        var customer = await database.Carts.Include(cart => cart.Items)
            .FirstOrDefaultAsync(cart => cart.CustomerId == customerId, cancellationToken);
        if (customer is null)
        {
            customer = NewCart(customerId, now);
            database.Carts.Add(customer);
            await database.SaveChangesAsync(cancellationToken);
        }

        var first = guest.Id.CompareTo(customer.Id) <= 0 ? guest.Id : customer.Id;
        var second = first == guest.Id ? customer.Id : guest.Id;
        await LockCartAsync(first, cancellationToken);
        if (second != first)
        {
            await LockCartAsync(second, cancellationToken);
        }

        if (guest.MergedIntoCartId is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        var variantIds = guest.Items.Select(item => item.VariantId)
            .Concat(customer.Items.Select(item => item.VariantId))
            .Distinct()
            .ToArray();
        var variants = await LoadVariantsAsync(variantIds, cancellationToken, tracking: false);
        var max = MaxQuantity();
        foreach (var item in guest.Items)
        {
            variants.TryGetValue(item.VariantId, out var variant);
            var existing = customer.Items.FirstOrDefault(line => line.VariantId == item.VariantId);
            var sum = (existing?.Quantity ?? 0) + item.Quantity;
            var active = variant is { IsActive: true, Product.Status: ProductStatus.Active };
            var available = variant?.Balance?.Available ?? 0;
            var cap = !active || available <= 0 ? max : Math.Min(available, max);
            var quantity = Math.Max(1, Math.Min(sum, cap));
            var limited = sum > quantity;
            if (existing is null)
            {
                database.CartItems.Add(new CartItem
                {
                    Id = Guid.CreateVersion7(),
                    CartId = customer.Id,
                    VariantId = item.VariantId,
                    Quantity = quantity,
                    QuantityLimited = limited,
                    CreatedAt = now,
                    UpdatedAt = now,
                });
            }
            else
            {
                existing.Quantity = quantity;
                existing.QuantityLimited = existing.QuantityLimited || limited;
                existing.UpdatedAt = now;
            }
        }

        guest.MergedIntoCartId = customer.Id;
        guest.UpdatedAt = now;
        customer.ExpiresAt = now.AddDays(Math.Max(1, options.Value.CartLifetimeDays));
        customer.UpdatedAt = now;
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<CommerceResult<QuoteModel>> QuoteAsync(
        Guid? customerId,
        byte[]? guestHash,
        CancellationToken cancellationToken)
    {
        var rate = ShippingRate();
        if (rate is null)
        {
            return CommerceResult<QuoteModel>.Fail(ShippingMissing());
        }

        var cart = await FindCartAsync(customerId, guestHash, tracking: false, cancellationToken);
        var priced = cart is null || cart.ExpiresAt <= time.GetUtcNow()
            ? []
            : await PriceAsync(cart.Items, cancellationToken);
        return CommerceResult<QuoteModel>.Ok(BuildQuote(priced, rate.Value));
    }

    private async Task<Cart?> FindCartAsync(Guid? customerId, byte[]? guestHash, bool tracking, CancellationToken cancellationToken)
    {
        IQueryable<Cart> query = database.Carts.Include(cart => cart.Items);
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        if (customerId is Guid id)
        {
            return await query.FirstOrDefaultAsync(cart => cart.CustomerId == id && cart.MergedIntoCartId == null, cancellationToken);
        }

        if (guestHash is null)
        {
            return null;
        }

        return await query.FirstOrDefaultAsync(
            cart => cart.TokenHash == guestHash && cart.CustomerId == null && cart.MergedIntoCartId == null,
            cancellationToken);
    }

    private Cart NewCart(Guid? customerId, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        CustomerId = customerId,
        ExpiresAt = now.AddDays(Math.Max(1, options.Value.CartLifetimeDays)),
        CreatedAt = now,
        UpdatedAt = now,
    };

    private async Task LockCartAsync(Guid cartId, CancellationToken cancellationToken)
    {
        await database.Database.SqlQuery<int>(
            $"""
            SELECT 1 AS "Value"
            FROM carts
            WHERE id = {cartId}
            FOR UPDATE
            """).ToListAsync(cancellationToken);
    }

    private async Task<bool> LockBalanceAsync(Guid variantId, CancellationToken cancellationToken)
    {
        var locked = await database.Database.SqlQuery<int>(
            $"""
            SELECT 1 AS "Value"
            FROM inventory_balances
            WHERE variant_id = {variantId}
            FOR UPDATE
            """).ToListAsync(cancellationToken);
        return locked.Count > 0;
    }

    private int MaxQuantity() => Math.Max(1, options.Value.MaxQuantityPerLine);

    private (decimal Fee, decimal Threshold)? ShippingRate()
    {
        if (options.Value.ShippingFee is not decimal fee || options.Value.FreeShippingThreshold is not decimal threshold)
        {
            return null;
        }

        return (fee, threshold);
    }

    private static CommerceFailure ShippingMissing() =>
        new(503, "Shipping is not configured.");

    private static CartModel EmptyCart() => new(null, 0, Money.Currency, [], null);

    private async Task<Dictionary<Guid, ProductVariant>> LoadVariantsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken,
        bool tracking = true)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var query = database.ProductVariants.AsQueryable();
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        return await query
            .Include(variant => variant.Product)
            .Include(variant => variant.Balance)
            .Include(variant => variant.Selections)
            .ThenInclude(selection => selection.OptionValue)
            .ThenInclude(value => value.Option)
            .Where(variant => ids.Contains(variant.Id))
            .ToDictionaryAsync(variant => variant.Id, cancellationToken);
    }

    private async Task<IReadOnlyList<PricedLine>> PriceAsync(IEnumerable<CartItem> items, CancellationToken cancellationToken)
    {
        var lines = items.ToArray();
        var variants = await LoadVariantsAsync(lines.Select(line => line.VariantId).ToArray(), cancellationToken);
        return lines
            .OrderBy(line => line.CreatedAt)
            .Select(line =>
            {
                variants.TryGetValue(line.VariantId, out var variant);
                return Price(line, variant);
            })
            .ToArray();
    }

    private static PricedLine Price(CartItem item, ProductVariant? variant)
    {
        var active = variant is { IsActive: true, Product.Status: ProductStatus.Active };
        var available = variant?.Balance?.Available ?? 0;
        string? reason = null;
        if (!active)
        {
            reason = "This product is no longer available.";
        }
        else if (available < item.Quantity)
        {
            reason = available <= 0 ? "Out of stock." : $"Only {available} left.";
        }

        var unit = variant?.Price ?? 0m;
        var options = variant is null
            ? []
            : variant.Selections
                .OrderBy(selection => selection.OptionValue.Option.Position)
                .ThenBy(selection => selection.OptionValue.Position)
                .Select(selection => new OptionPair(selection.OptionValue.Option.Name, selection.OptionValue.Value))
                .ToArray();
        return new PricedLine(
            item.Id,
            item.VariantId,
            variant?.Product.Name ?? "Unavailable product",
            variant?.Sku ?? string.Empty,
            options,
            unit,
            item.Quantity,
            available,
            reason is null,
            reason,
            item.QuantityLimited);
    }

    private QuoteModel BuildQuote(IReadOnlyList<PricedLine> lines, (decimal Fee, decimal Threshold) rate)
    {
        var merchandise = lines.Aggregate(0m, (sum, line) => sum + CommerceMath.LineTotal(line.UnitPrice, line.Quantity));
        var shipping = lines.Count == 0 ? 0m : CommerceMath.Shipping(merchandise, rate.Fee, rate.Threshold);
        return new QuoteModel(
            Money.Currency,
            CommerceMoney.Format(merchandise),
            CommerceMoney.Format(shipping),
            CommerceMoney.Format(merchandise + shipping),
            Math.Max(0, options.Value.PendingPaymentMinutes),
            CommerceMoney.Format(rate.Fee),
            CommerceMoney.Format(rate.Threshold),
            lines.Count > 0 && lines.All(line => line.Purchasable),
            lines.Select(line => new QuoteLineModel(
                line.VariantId,
                line.ProductName,
                line.Sku,
                line.Options,
                CommerceMoney.Format(line.UnitPrice),
                line.Quantity,
                CommerceMoney.Format(CommerceMath.LineTotal(line.UnitPrice, line.Quantity)),
                line.Purchasable,
                line.UnavailableReason)).ToArray());
    }

    private static CartModel MapCart(Guid id, DateTimeOffset expiresAt, IReadOnlyList<PricedLine> lines) => new(
        id,
        lines.Sum(line => line.Quantity),
        Money.Currency,
        lines.Select(line => new CartLineModel(
            line.ItemId,
            line.VariantId,
            line.ProductName,
            line.Sku,
            line.Options,
            CommerceMoney.Format(line.UnitPrice),
            line.Quantity,
            CommerceMoney.Format(CommerceMath.LineTotal(line.UnitPrice, line.Quantity)),
            line.Available,
            line.Purchasable,
            line.UnavailableReason,
            line.QuantityLimited)).ToArray(),
        expiresAt);

    private sealed record PricedLine(
        Guid ItemId,
        Guid VariantId,
        string ProductName,
        string Sku,
        IReadOnlyList<OptionPair> Options,
        decimal UnitPrice,
        int Quantity,
        int Available,
        bool Purchasable,
        string? UnavailableReason,
        bool QuantityLimited);
}
