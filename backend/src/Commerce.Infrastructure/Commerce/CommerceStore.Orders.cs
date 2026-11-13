using System.Security.Cryptography;
using System.Text.Json;
using Commerce.Application.Commerce;
using Commerce.Application.Identity;
using Commerce.Domain.Catalog;
using Commerce.Domain.Commerce;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Commerce.Infrastructure.Ordering;

public sealed partial class CommerceStore
{
    private static readonly string WorkerId = CreateWorkerId();

    public async Task<CommerceResult<PlacedOrderModel>> SubmitAsync(
        Guid? customerId,
        byte[]? guestHash,
        SubmitCommand command,
        CancellationToken cancellationToken)
    {
        if (customerId is null && guestHash is null)
        {
            return CommerceResult<PlacedOrderModel>.Fail(new CommerceFailure(409, "Your bag changed. Review it and try again."));
        }

        var max = MaxQuantity();
        if (command.Lines.Any(line => line.Quantity > max))
        {
            return CommerceResult<PlacedOrderModel>.Invalid(new Dictionary<string, string[]>
            {
                ["lines"] = [$"Each quantity must be from 1 to {max}."],
            });
        }

        var rate = ShippingRate();
        if (rate is null)
        {
            return CommerceResult<PlacedOrderModel>.Fail(ShippingMissing());
        }

        var scope = customerId is Guid owner
            ? $"c:{owner:N}"
            : $"g:{Convert.ToHexString(guestHash!).ToLowerInvariant()}";
        var now = time.GetUtcNow();
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var submission = new OrderSubmission
        {
            Id = Guid.CreateVersion7(),
            Scope = scope,
            IdempotencyKey = command.IdempotencyKey,
            Fingerprint = command.Fingerprint,
            CreatedAt = now,
        };
        database.OrderSubmissions.Add(submission);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUnique(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            database.ChangeTracker.Clear();
            return await ReplayAsync(scope, command, customerId, guestHash, cancellationToken);
        }

        var cart = await FindCartAsync(customerId, guestHash, tracking: true, cancellationToken);
        if (cart is not null)
        {
            await database.Database.SqlQuery<Guid>(
                $"""
                SELECT id AS "Value"
                FROM carts
                WHERE id = {cart.Id}
                FOR UPDATE
                """).ToListAsync(cancellationToken);
            foreach (var item in cart.Items.ToList())
            {
                database.Entry(item).State = EntityState.Detached;
            }

            cart.Items.Clear();
            var items = database.Entry(cart).Collection(item => item.Items);
            items.IsLoaded = false;
            await items.LoadAsync(cancellationToken);
        }

        var cartLines = cart is null || cart.ExpiresAt <= now
            ? []
            : cart.Items.OrderBy(item => item.VariantId).Select(item => (item.VariantId, item.Quantity)).ToArray();
        var intent = command.Lines.OrderBy(line => line.VariantId).Select(line => (line.VariantId, line.Quantity)).ToArray();
        if (cart is null || !cartLines.SequenceEqual(intent))
        {
            await RollbackAsync(transaction, cancellationToken);
            return CommerceResult<PlacedOrderModel>.Fail(new CommerceFailure(409, "Your bag changed. Review it and try again."));
        }

        foreach (var variantId in intent.Select(line => line.VariantId))
        {
            if (!await LockBalanceAsync(variantId, cancellationToken))
            {
                await RollbackAsync(transaction, cancellationToken);
                return CommerceResult<PlacedOrderModel>.Fail(new CommerceFailure(409, "A product in your bag is no longer available."));
            }
        }

        var variants = await LoadVariantsAsync(intent.Select(line => line.VariantId).ToArray(), cancellationToken);
        var priced = cart.Items.Select(item => Price(item, variants.GetValueOrDefault(item.VariantId))).ToArray();
        if (priced.Any(line => !line.Purchasable))
        {
            await RollbackAsync(transaction, cancellationToken);
            return CommerceResult<PlacedOrderModel>.Fail(new CommerceFailure(409, "A product in your bag is no longer available."));
        }

        var merchandise = priced.Aggregate(0m, (sum, line) => sum + CommerceMath.LineTotal(line.UnitPrice, line.Quantity));
        var shipping = CommerceMath.Shipping(merchandise, rate.Value.Fee, rate.Value.Threshold);
        var grand = merchandise + shipping;
        if (grand != command.ExpectedGrandTotal)
        {
            await RollbackAsync(transaction, cancellationToken);
            return CommerceResult<PlacedOrderModel>.Fail(new CommerceFailure(
                409,
                "The total changed. Review the new total before placing the order."));
        }

        var order = new CustomerOrder
        {
            Id = Guid.CreateVersion7(),
            Reference = await NewReferenceAsync(cancellationToken),
            CustomerId = customerId,
            Email = command.Email,
            Phone = command.Phone,
            Status = OrderStatus.PaymentPending,
            Currency = Money.Currency,
            MerchandiseTotal = merchandise,
            ShippingTotal = shipping,
            GrandTotal = grand,
            ExpiresAt = now.AddMinutes(Math.Max(0, options.Value.PendingPaymentMinutes)),
            CreatedAt = now,
            UpdatedAt = now,
        };
        foreach (var line in priced)
        {
            order.Items.Add(new CustomerOrderItem
            {
                Id = Guid.CreateVersion7(),
                OrderId = order.Id,
                VariantId = line.VariantId,
                ProductName = line.ProductName,
                Sku = line.Sku,
                OptionsJson = JsonSerializer.Serialize(line.Options, JsonOptions),
                UnitPrice = line.UnitPrice,
                Quantity = line.Quantity,
                LineTotal = CommerceMath.LineTotal(line.UnitPrice, line.Quantity),
            });
            var balance = variants[line.VariantId].Balance!;
            balance.Reserved += line.Quantity;
            database.InventoryMovements.Add(Movement(
                line.VariantId,
                InventoryMovementType.Reservation,
                line.Quantity,
                balance,
                $"{order.Id:N}:reserve",
                command.Fingerprint,
                now));
        }

        order.Addresses.Add(Address(order.Id, OrderAddressKind.Shipping, command.Shipping));
        order.Addresses.Add(Address(order.Id, OrderAddressKind.Billing, command.Billing));
        order.History.Add(new OrderStatusChange
        {
            Id = Guid.CreateVersion7(),
            OrderId = order.Id,
            ToStatus = OrderStatus.PaymentPending,
            Actor = customerId is null ? "guest" : $"customer:{customerId:N}",
            CreatedAt = now,
        });
        var rawToken = SecureTokens.Create();
        database.Orders.Add(order);
        database.OrderAccessTokens.Add(new OrderAccessToken
        {
            Id = Guid.CreateVersion7(),
            OrderId = order.Id,
            TokenHash = SecureTokens.Hash(rawToken),
            ExpiresAt = now.AddDays(Math.Max(1, options.Value.OrderAccessDays)),
            CreatedAt = now,
        });
        database.EmailOutbox.Add(Outbox(order, OutboxMessageType.Received, now));
        submission.OrderId = order.Id;
        database.CartItems.RemoveRange(cart.Items);
        string? confirmationToken = null;
        DateTimeOffset? confirmationExpires = null;
        if (guestHash is not null)
        {
            (confirmationToken, confirmationExpires) = await AttachConfirmationAsync(
                guestHash, order.Id, command.ConfirmationCookie, now, cancellationToken);
        }

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        CommerceMetrics.OrderPlaced();
        return CommerceResult<PlacedOrderModel>.Ok(new PlacedOrderModel(
            MapOrder(order),
            confirmationToken,
            confirmationExpires,
            Math.Max(1, options.Value.ConfirmationSessionHours)));
    }

    public async Task<IReadOnlyList<OrderModel>> ListForCustomerAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var orders = await OrderQuery()
            .Where(order => order.CustomerId == customerId)
            .OrderByDescending(order => order.CreatedAt)
            .ToListAsync(cancellationToken);
        return orders.Select(MapOrder).ToArray();
    }

    public async Task<OrderModel?> FindForCustomerAsync(Guid customerId, string reference, CancellationToken cancellationToken)
    {
        var order = await OrderQuery().FirstOrDefaultAsync(
            item => item.CustomerId == customerId && item.Reference == reference,
            cancellationToken);
        return order is null ? null : MapOrder(order);
    }

    public async Task<OrderModel?> FindByAccessTokenAsync(string rawToken, CancellationToken cancellationToken)
    {
        var hash = SecureTokens.Hash(rawToken);
        var now = time.GetUtcNow();
        var token = await database.OrderAccessTokens.AsNoTracking()
            .FirstOrDefaultAsync(item => item.TokenHash == hash && item.ExpiresAt > now, cancellationToken);
        if (token is null)
        {
            return null;
        }

        var order = await OrderQuery().FirstOrDefaultAsync(item => item.Id == token.OrderId, cancellationToken);
        return order is null ? null : MapOrder(order);
    }

    public async Task<ConfirmationPageModel?> FindByConfirmationAsync(string? rawCookie, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawCookie))
        {
            return null;
        }

        var now = time.GetUtcNow();
        var hash = SecureTokens.Hash(rawCookie);
        var session = await database.OrderConfirmationSessions.AsNoTracking()
            .FirstOrDefaultAsync(item => item.TokenHash == hash && item.ExpiresAt > now, cancellationToken);
        if (session is null)
        {
            return null;
        }

        var ids = await database.OrderConfirmationSessionOrders.AsNoTracking()
            .Where(link => link.SessionId == session.Id)
            .OrderByDescending(link => link.CreatedAt)
            .Select(link => link.OrderId)
            .ToListAsync(cancellationToken);
        var orders = await OrderQuery().Where(order => ids.Contains(order.Id)).ToListAsync(cancellationToken);
        var ordered = ids.Select(id => orders.First(order => order.Id == id)).Select(MapOrder).ToArray();
        return new ConfirmationPageModel(ordered, Math.Max(1, options.Value.ConfirmationSessionHours));
    }

    public async Task<CommerceResult<AdminOrderPageModel>> ListAdminAsync(
        string? status,
        string? reference,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var rate = ShippingRate();
        if (rate is null)
        {
            return CommerceResult<AdminOrderPageModel>.Fail(ShippingMissing());
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        IQueryable<CustomerOrder> query = database.Orders.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<OrderStatus>(status, true, out var parsed) || parsed == OrderStatus.Paid)
            {
                return CommerceResult<AdminOrderPageModel>.Invalid(new Dictionary<string, string[]>
                {
                    ["status"] = ["Filter by paymentpending, cancelled, or expired."],
                });
            }

            query = query.Where(order => order.Status == parsed);
        }

        if (!string.IsNullOrWhiteSpace(reference))
        {
            var term = reference.Trim();
            query = query.Where(order => order.Reference.Contains(term));
        }

        var total = await query.CountAsync(cancellationToken);
        var pageRows = await query
            .OrderByDescending(order => order.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(order => new
            {
                order.Id,
                order.Reference,
                order.Status,
                order.Email,
                order.GrandTotal,
                order.CreatedAt,
                order.ExpiresAt,
            })
            .ToListAsync(cancellationToken);
        var rows = pageRows.Select(order => new AdminOrderRowModel(
            order.Id,
            order.Reference,
            order.Status.ToString().ToLowerInvariant(),
            order.Email,
            CommerceMoney.Format(order.GrandTotal),
            order.CreatedAt,
            order.ExpiresAt)).ToArray();
        return CommerceResult<AdminOrderPageModel>.Ok(new AdminOrderPageModel(
            rows,
            page,
            pageSize,
            total,
            CommerceMoney.Format(rate.Value.Fee),
            CommerceMoney.Format(rate.Value.Threshold),
            Math.Max(0, options.Value.PendingPaymentMinutes)));
    }

    public async Task<OrderModel?> FindAdminAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await OrderQuery().FirstOrDefaultAsync(item => item.Id == orderId, cancellationToken);
        return order is null ? null : MapOrder(order);
    }

    public async Task<CommerceResult<OrderModel>> CancelAsync(
        Guid orderId,
        Guid actorId,
        string? reason,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var locked = await database.Database.SqlQuery<Guid>(
            $"""
            SELECT id AS "Value"
            FROM orders
            WHERE id = {orderId}
            FOR UPDATE
            """).ToListAsync(cancellationToken);
        if (locked.Count == 0)
        {
            await RollbackAsync(transaction, cancellationToken);
            return CommerceResult<OrderModel>.Fail(new CommerceFailure(404, "The order was not found."));
        }

        var order = await database.Orders.Include(item => item.Items).SingleAsync(item => item.Id == orderId, cancellationToken);
        if (order.Status != OrderStatus.PaymentPending)
        {
            await RollbackAsync(transaction, cancellationToken);
            return CommerceResult<OrderModel>.Fail(new CommerceFailure(409, "This order can no longer be cancelled."));
        }

        var now = time.GetUtcNow();
        await ReleaseAsync(order, now, cancellationToken);
        order.Status = OrderStatus.Cancelled;
        order.UpdatedAt = now;
        database.OrderStatusHistory.Add(new OrderStatusChange
        {
            Id = Guid.CreateVersion7(),
            OrderId = order.Id,
            FromStatus = OrderStatus.PaymentPending,
            ToStatus = OrderStatus.Cancelled,
            Actor = $"admin:{actorId:N}",
            Reason = reason,
            CreatedAt = now,
        });
        database.EmailOutbox.Add(Outbox(order, OutboxMessageType.Cancelled, now));
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var loaded = await OrderQuery().SingleAsync(item => item.Id == order.Id, cancellationToken);
        return CommerceResult<OrderModel>.Ok(MapOrder(loaded));
    }

    private async Task<CommerceResult<PlacedOrderModel>> ReplayAsync(
        string scope,
        SubmitCommand command,
        Guid? customerId,
        byte[]? guestHash,
        CancellationToken cancellationToken)
    {
        var existing = await database.OrderSubmissions.AsNoTracking().FirstOrDefaultAsync(
            submission => submission.Scope == scope && submission.IdempotencyKey == command.IdempotencyKey,
            cancellationToken);
        if (existing?.OrderId is not Guid orderId)
        {
            return CommerceResult<PlacedOrderModel>.Fail(new CommerceFailure(409, "Checkout was interrupted. Submit the order again."));
        }

        if (!string.Equals(existing.Fingerprint, command.Fingerprint, StringComparison.Ordinal))
        {
            return CommerceResult<PlacedOrderModel>.Fail(new CommerceFailure(
                409,
                "That idempotency key was already used for a different checkout."));
        }

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var order = await OrderQuery().FirstAsync(item => item.Id == orderId, cancellationToken);
        await IssueAccessTokenAsync(order.Id, cancellationToken);
        string? confirmationToken = null;
        DateTimeOffset? confirmationExpires = null;
        if (customerId is null && guestHash is not null)
        {
            (confirmationToken, confirmationExpires) = await AttachConfirmationAsync(
                guestHash, order.Id, command.ConfirmationCookie, time.GetUtcNow(), cancellationToken);
        }

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return CommerceResult<PlacedOrderModel>.Ok(new PlacedOrderModel(
            MapOrder(order),
            confirmationToken,
            confirmationExpires,
            Math.Max(1, options.Value.ConfirmationSessionHours)));
    }

    private async Task<(string? Token, DateTimeOffset? Expires)> AttachConfirmationAsync(
        byte[] guestHash,
        Guid orderId,
        string? presented,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var hours = Math.Max(1, options.Value.ConfirmationSessionHours);
        var expires = now.AddHours(hours);
        OrderConfirmationSession? session = null;
        if (!string.IsNullOrWhiteSpace(presented))
        {
            var hash = SecureTokens.Hash(presented);
            var locked = await database.Database.SqlQuery<Guid>(
                $"""
                SELECT id AS "Value"
                FROM order_confirmation_sessions
                WHERE token_hash = {hash}
                FOR UPDATE
                """).ToListAsync(cancellationToken);
            if (locked.Count == 1)
            {
                var candidate = await database.OrderConfirmationSessions.SingleAsync(item => item.Id == locked[0], cancellationToken);
                if (candidate.ExpiresAt > now && candidate.GuestCartHash.AsSpan().SequenceEqual(guestHash))
                {
                    session = candidate;
                }
            }
        }

        string? issued = null;
        if (session is null)
        {
            issued = SecureTokens.Create();
            session = new OrderConfirmationSession
            {
                Id = Guid.CreateVersion7(),
                TokenHash = SecureTokens.Hash(issued),
                GuestCartHash = guestHash.ToArray(),
                ExpiresAt = expires,
                CreatedAt = now,
            };
            database.OrderConfirmationSessions.Add(session);
        }

        var linked = await database.OrderConfirmationSessionOrders.AnyAsync(
            link => link.SessionId == session.Id && link.OrderId == orderId,
            cancellationToken);
        if (!linked)
        {
            database.OrderConfirmationSessionOrders.Add(new OrderConfirmationSessionOrder
            {
                SessionId = session.Id,
                OrderId = orderId,
                CreatedAt = now,
            });
        }

        return (issued, issued is null ? null : expires);
    }

    private async Task<string> IssueAccessTokenAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var raw = SecureTokens.Create();
        database.OrderAccessTokens.Add(new OrderAccessToken
        {
            Id = Guid.CreateVersion7(),
            OrderId = orderId,
            TokenHash = SecureTokens.Hash(raw),
            ExpiresAt = now.AddDays(Math.Max(1, options.Value.OrderAccessDays)),
            CreatedAt = now,
        });
        await database.SaveChangesAsync(cancellationToken);
        return raw;
    }

    private async Task ReleaseAsync(CustomerOrder order, DateTimeOffset now, CancellationToken cancellationToken)
    {
        foreach (var item in order.Items.OrderBy(line => line.VariantId))
        {
            if (!await LockBalanceAsync(item.VariantId, cancellationToken))
            {
                continue;
            }

            var balance = await database.InventoryBalances.SingleAsync(entry => entry.VariantId == item.VariantId, cancellationToken);
            balance.Reserved -= item.Quantity;
            database.InventoryMovements.Add(Movement(
                item.VariantId,
                InventoryMovementType.ReservationRelease,
                -item.Quantity,
                balance,
                $"{order.Id:N}:release",
                "release",
                now));
        }

        CommerceMetrics.ReservationReleased();
    }

    private static InventoryMovement Movement(
        Guid variantId,
        InventoryMovementType type,
        int reservedDelta,
        InventoryBalance balance,
        string key,
        string hash,
        DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        VariantId = variantId,
        MovementType = type,
        QuantityDelta = 0,
        ReservedDelta = reservedDelta,
        OnHandAfter = balance.OnHand,
        ReservedAfter = balance.Reserved,
        IdempotencyKey = key,
        RequestHash = hash,
        CreatedAt = now,
    };

    private static CustomerOrderAddress Address(Guid orderId, OrderAddressKind kind, PostalAddress source) => new()
    {
        Id = Guid.CreateVersion7(),
        OrderId = orderId,
        Kind = kind,
        Recipient = source.Recipient,
        Line1 = source.Line1,
        Line2 = source.Line2,
        District = source.District,
        City = source.City,
        PostalCode = source.PostalCode,
        Country = source.Country,
        Notes = source.Notes,
    };

    private static EmailOutbox Outbox(CustomerOrder order, OutboxMessageType type, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        OrderId = order.Id,
        MessageType = type,
        Payload = JsonSerializer.Serialize(new OutboxBody(
            order.Email,
            order.Reference,
            type.ToString().ToLowerInvariant(),
            order.ExpiresAt.ToString("O")), JsonOptions),
        Attempts = 0,
        NextAttemptAt = now,
        CreatedAt = now,
    };

    private IQueryable<CustomerOrder> OrderQuery() =>
        database.Orders.AsNoTracking()
            .Include(order => order.Items)
            .Include(order => order.Addresses)
            .Include(order => order.History);

    private OrderModel MapOrder(CustomerOrder order) => new(
        order.Id,
        order.Reference,
        order.Status.ToString().ToLowerInvariant(),
        order.Email,
        order.Phone,
        order.Currency,
        CommerceMoney.Format(order.MerchandiseTotal),
        CommerceMoney.Format(order.ShippingTotal),
        CommerceMoney.Format(order.GrandTotal),
        order.ExpiresAt,
        order.CreatedAt,
        order.Status == OrderStatus.Paid,
        Math.Max(0, options.Value.PendingPaymentMinutes),
        order.Items.Select(item => new OrderLineModel(
            item.VariantId,
            item.ProductName,
            item.Sku,
            JsonSerializer.Deserialize<List<OptionPair>>(item.OptionsJson, JsonOptions) ?? [],
            CommerceMoney.Format(item.UnitPrice),
            item.Quantity,
            CommerceMoney.Format(item.LineTotal))).ToArray(),
        order.Addresses.OrderBy(address => address.Kind).Select(address => new OrderAddressModel(
            address.Kind.ToString().ToLowerInvariant(),
            address.Recipient,
            address.Line1,
            address.Line2,
            address.District,
            address.City,
            address.PostalCode,
            address.Country,
            address.Notes)).ToArray(),
        order.History.OrderBy(change => change.CreatedAt).Select(change => new OrderHistoryModel(
            change.FromStatus?.ToString().ToLowerInvariant(),
            change.ToStatus.ToString().ToLowerInvariant(),
            change.Actor,
            change.Reason,
            change.CreatedAt)).ToArray());

    private async Task<string> NewReferenceAsync(CancellationToken cancellationToken)
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var bytes = RandomNumberGenerator.GetBytes(8);
            var chars = new char[8];
            for (var index = 0; index < chars.Length; index++)
            {
                chars[index] = alphabet[bytes[index] % alphabet.Length];
            }

            var reference = "SW-" + new string(chars);
            if (!await database.Orders.AnyAsync(order => order.Reference == reference, cancellationToken))
            {
                return reference;
            }
        }

        throw new InvalidOperationException("Could not allocate an order reference.");
    }

    private async Task RollbackAsync(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction, CancellationToken cancellationToken)
    {
        await transaction.RollbackAsync(cancellationToken);
        database.ChangeTracker.Clear();
    }

    public async Task<int> ExpireDueAsync(CancellationToken cancellationToken)
    {
        var released = 0;
        var now = time.GetUtcNow();
        var candidates = await database.Orders.AsNoTracking()
            .Where(order => order.Status == OrderStatus.PaymentPending && order.ExpiresAt <= now)
            .OrderBy(order => order.ExpiresAt)
            .Select(order => order.Id)
            .Take(20)
            .ToListAsync(cancellationToken);
        foreach (var orderId in candidates)
        {
            await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
            var locked = await database.Database.SqlQuery<Guid>(
                $"""
                SELECT id AS "Value"
                FROM orders
                WHERE id = {orderId}
                  AND status = 'paymentpending'
                  AND expires_at <= {now}
                FOR UPDATE SKIP LOCKED
                """).ToListAsync(cancellationToken);
            if (locked.Count == 0)
            {
                await RollbackAsync(transaction, cancellationToken);
                continue;
            }

            var order = await database.Orders.Include(item => item.Items).SingleAsync(item => item.Id == orderId, cancellationToken);
            if (order.Status != OrderStatus.PaymentPending || order.ExpiresAt > now)
            {
                await RollbackAsync(transaction, cancellationToken);
                continue;
            }

            await ReleaseAsync(order, now, cancellationToken);
            order.Status = OrderStatus.Expired;
            order.UpdatedAt = now;
            database.OrderStatusHistory.Add(new OrderStatusChange
            {
                Id = Guid.CreateVersion7(),
                OrderId = order.Id,
                FromStatus = OrderStatus.PaymentPending,
                ToStatus = OrderStatus.Expired,
                Actor = "system",
                Reason = "Pending payment expired.",
                CreatedAt = now,
            });
            database.EmailOutbox.Add(Outbox(order, OutboxMessageType.Expired, now));
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            database.ChangeTracker.Clear();
            released++;
        }

        var cutoff = now.AddDays(-7);
        await database.OrderSubmissions.Where(submission => submission.CreatedAt < cutoff).ExecuteDeleteAsync(cancellationToken);
        return released;
    }

    public async Task<int> DispatchOutboxAsync(CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var until = now.AddMinutes(2);
        await database.Database.ExecuteSqlAsync(
            $"""
            UPDATE email_outbox
            SET locked_by = {WorkerId}, locked_until = {until}
            WHERE id IN (
                SELECT id
                FROM email_outbox
                WHERE sent_at IS NULL
                  AND next_attempt_at <= {now}
                  AND attempts < 8
                  AND (locked_until IS NULL OR locked_until <= {now})
                ORDER BY next_attempt_at
                LIMIT 5
                FOR UPDATE SKIP LOCKED
            )
            """,
            cancellationToken);
        var claimed = await database.EmailOutbox
            .Where(message => message.LockedBy == WorkerId && message.LockedUntil == until && message.SentAt == null)
            .ToListAsync(cancellationToken);
        var sent = 0;
        foreach (var message in claimed)
        {
            try
            {
                var body = JsonSerializer.Deserialize<OutboxBody>(message.Payload, JsonOptions)
                    ?? throw new InvalidOperationException("smtp-failed");
                await email.SendAsync(new EmailMessage(body.To, $"Order {body.Reference}", EmailText(body)), cancellationToken);
                message.SentAt = time.GetUtcNow();
                message.LockedBy = null;
                message.LockedUntil = null;
                message.LastError = null;
                sent++;
                CommerceMetrics.OutboxMessageSent();
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                CommerceMetrics.OutboxMessageFailed();
                message.Attempts += 1;
                message.NextAttemptAt = time.GetUtcNow().Add(Backoff(message.Attempts));
                message.LastError = FailureCode(exception);
                message.LockedBy = null;
                message.LockedUntil = null;
            }

            await database.SaveChangesAsync(cancellationToken);
        }

        return sent;
    }

    private static string EmailText(OutboxBody body) => body.Kind switch
    {
        "cancelled" => $"Order {body.Reference} was cancelled. Payment has not been collected. Reserved stock has been released.",
        "expired" => $"Order {body.Reference} expired. Payment has not been collected. Reserved stock has been released.",
        _ => $"We received order {body.Reference}. Payment has not been collected. The reservation expires at {body.ExpiresAt} and reserved stock is released then.",
    };

    private static TimeSpan Backoff(int attempts) => attempts switch
    {
        <= 1 => TimeSpan.FromMinutes(1),
        2 => TimeSpan.FromMinutes(2),
        3 => TimeSpan.FromMinutes(4),
        4 => TimeSpan.FromMinutes(8),
        5 => TimeSpan.FromMinutes(16),
        _ => TimeSpan.FromMinutes(30),
    };

    private static string FailureCode(Exception exception)
    {
        if (exception is MailKit.Net.Smtp.SmtpCommandException smtp)
        {
            return $"smtp-{(int)smtp.StatusCode}";
        }

        return System.Text.RegularExpressions.Regex.IsMatch(exception.Message, "^smtp-\\d{3}$")
            ? exception.Message
            : "smtp-failed";
    }

    private static bool IsUnique(DbUpdateException exception) =>
        FindPostgres(exception)?.SqlState == PostgresErrorCodes.UniqueViolation;

    private static PostgresException? FindPostgres(Exception exception)
    {
        for (var current = exception as Exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres)
            {
                return postgres;
            }
        }

        return null;
    }

    private static string CreateWorkerId()
    {
        var value = $"{Environment.MachineName}:{Guid.NewGuid():N}";
        return value.Length <= 80 ? value : value[..80];
    }

    private sealed record OutboxBody(string To, string Reference, string Kind, string ExpiresAt);
}
