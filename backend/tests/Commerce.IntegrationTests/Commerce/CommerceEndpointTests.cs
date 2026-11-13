using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Commerce.Application.Commerce;
using Commerce.Domain.Catalog;
using Commerce.Domain.Commerce;
using Commerce.Infrastructure.Persistence;
using Commerce.IntegrationTests.Catalog;
using Commerce.IntegrationTests.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Commerce.IntegrationTests.Orders;

[Collection(CommerceCollection.Name)]
public sealed class CommerceEndpointTests(CommerceApiFactory factory)
{
    [Fact]
    public async Task Guest_cart_is_private_and_authenticated_cart_merges_with_a_cap()
    {
        var admin = await CatalogHttp.AdminClientAsync(factory);
        var variant = await CreateVariantAsync(admin, "Merge tee", "100.00", 20);
        var guest = factory.CreateClient();
        var added = await AddAsync(guest, variant, 6);
        Assert.Equal(6, added.GetProperty("itemCount").GetInt32());
        var other = factory.CreateClient();
        var empty = await other.GetFromJsonAsync<JsonElement>("/api/v1/cart");
        Assert.Equal(0, empty.GetProperty("itemCount").GetInt32());

        var email = AuthHttp.EmailAddress();
        var registered = await AuthHttp.RegisterAsync(guest, email);
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        var owned = await guest.GetFromJsonAsync<JsonElement>("/api/v1/cart");
        Assert.Equal(6, owned.GetProperty("items")[0].GetProperty("quantity").GetInt32());

        var returning = factory.CreateClient();
        await AddAsync(returning, variant, 6);
        var loggedIn = await AuthHttp.LoginAsync(returning, email);
        Assert.Equal(HttpStatusCode.OK, loggedIn.StatusCode);
        var merged = await returning.GetFromJsonAsync<JsonElement>("/api/v1/cart");
        Assert.Equal(10, merged.GetProperty("items")[0].GetProperty("quantity").GetInt32());
        Assert.True(merged.GetProperty("items")[0].GetProperty("quantityLimited").GetBoolean());
    }

    [Fact]
    public async Task Adding_a_second_line_to_an_existing_cart_succeeds()
    {
        var admin = await CatalogHttp.AdminClientAsync(factory);
        var first = await CreateVariantAsync(admin, "First line", "10.00", 5);
        var second = await CreateVariantAsync(admin, "Second line", "20.00", 5);
        var client = factory.CreateClient();
        await AddAsync(client, first, 1);
        var cart = await AddAsync(client, second, 1);
        Assert.Equal(2, cart.GetProperty("itemCount").GetInt32());
    }

    [Fact]
    public async Task Quote_uses_server_prices_and_the_approved_shipping_rules()
    {
        var admin = await CatalogHttp.AdminClientAsync(factory);
        var priced = await CreateVariantAsync(admin, "Fee tee", "1000.00", 5);
        var free = await CreateVariantAsync(admin, "Free tee", "2500.00", 5);
        var client = factory.CreateClient();
        await AddAsync(client, priced, 1);
        var quote = await QuoteAsync(client);
        Assert.Equal("1000.00", quote.GetProperty("merchandiseTotal").GetString());
        Assert.Equal("149.00", quote.GetProperty("shippingTotal").GetString());
        Assert.Equal("1149.00", quote.GetProperty("grandTotal").GetString());
        Assert.Equal(30, quote.GetProperty("reservationExpiresInMinutes").GetInt32());

        var freeClient = factory.CreateClient();
        await AddAsync(freeClient, free, 1);
        var freeQuote = await QuoteAsync(freeClient);
        Assert.Equal("0.00", freeQuote.GetProperty("shippingTotal").GetString());
        Assert.Equal("2500.00", freeQuote.GetProperty("grandTotal").GetString());
    }

    [Fact]
    public async Task Stale_quote_does_not_commit_and_the_same_key_can_be_retried()
    {
        var admin = await CatalogHttp.AdminClientAsync(factory);
        var variant = await CreateVariantAsync(admin, "Stale tee", "1000.00", 4);
        var client = factory.CreateClient();
        var cart = await AddAsync(client, variant, 1);
        var quote = await QuoteAsync(client);
        var changed = await CatalogHttp.PatchAsync(admin, $"/api/v1/admin/variants/{variant}", new { price = "1100.00" });
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        var key = CatalogHttp.Token();
        var stale = await PlaceAsync(client, key, quote.GetProperty("grandTotal").GetString()!, Lines(cart));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(0, await ReservedAsync(variant));
        Assert.Equal(0, await SubmissionCountAsync(key));
        var fresh = await QuoteAsync(client);
        var placed = await PlaceAsync(client, key, fresh.GetProperty("grandTotal").GetString()!, Lines(cart));
        Assert.Equal(HttpStatusCode.Created, placed.StatusCode);
        Assert.Equal("1249.00", (await placed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("order").GetProperty("grandTotal").GetString());
    }

    [Fact]
    public async Task Replay_returns_the_original_order_without_reserving_again()
    {
        var admin = await CatalogHttp.AdminClientAsync(factory);
        var variant = await CreateVariantAsync(admin, "Replay tee", "800.00", 3);
        var client = factory.CreateClient();
        var cart = await AddAsync(client, variant, 1);
        var quote = await QuoteAsync(client);
        var key = CatalogHttp.Token();
        var lines = Lines(cart);
        var firstResponse = await PlaceAsync(client, key, quote.GetProperty("grandTotal").GetString()!, lines);
        var first = await ReadAsync(firstResponse);
        Assert.False(first.TryGetProperty("accessToken", out _));
        var reference = first.GetProperty("order").GetProperty("reference").GetString();
        var empty = await client.GetFromJsonAsync<JsonElement>("/api/v1/cart");
        Assert.Equal(0, empty.GetProperty("itemCount").GetInt32());
        var second = await ReadAsync(await PlaceAsync(client, key, quote.GetProperty("grandTotal").GetString()!, lines));
        Assert.Equal(reference, second.GetProperty("order").GetProperty("reference").GetString());
        Assert.False(second.TryGetProperty("accessToken", out _));
        Assert.Equal(1, await ReservedAsync(variant));
        var viewed = await client.GetFromJsonAsync<JsonElement>("/api/v1/orders/confirmation");
        Assert.Equal(reference, viewed.GetProperty("orders")[0].GetProperty("reference").GetString());
    }

    [Fact]
    public async Task Fingerprint_conflict_does_not_create_another_order()
    {
        var admin = await CatalogHttp.AdminClientAsync(factory);
        var variant = await CreateVariantAsync(admin, "Conflict tee", "700.00", 3);
        var client = factory.CreateClient();
        var cart = await AddAsync(client, variant, 1);
        var quote = await QuoteAsync(client);
        var key = CatalogHttp.Token();
        var placed = await PlaceAsync(client, key, quote.GetProperty("grandTotal").GetString()!, Lines(cart));
        Assert.Equal(HttpStatusCode.Created, placed.StatusCode);
        var conflict = await PlaceAsync(client, key, quote.GetProperty("grandTotal").GetString()!, Lines(cart), phone: "+905550000099");
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal(1, await ReservedAsync(variant));
        Assert.Equal(1, await OrdersForVariantAsync(variant));
    }

    [Fact]
    public async Task Concurrent_same_key_creates_one_order()
    {
        var admin = await CatalogHttp.AdminClientAsync(factory);
        var variant = await CreateVariantAsync(admin, "Race key tee", "600.00", 3);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var cart = await AddAsync(client, variant, 1);
        var quote = await QuoteAsync(client);
        var key = CatalogHttp.Token();
        var total = quote.GetProperty("grandTotal").GetString()!;
        var lines = Lines(cart);
        var responses = await Task.WhenAll(PlaceAsync(client, key, total, lines), PlaceAsync(client, key, total, lines));
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
        var references = new List<string>();
        foreach (var response in responses)
        {
            references.Add((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("order").GetProperty("reference").GetString()!);
        }

        Assert.Equal(references[0], references[1]);
        Assert.Equal(1, await ReservedAsync(variant));
    }

    [Fact]
    public async Task Last_unit_can_be_reserved_by_only_one_order()
    {
        var admin = await CatalogHttp.AdminClientAsync(factory);
        var variant = await CreateVariantAsync(admin, "Last tee", "500.00", 1);
        var first = factory.CreateClient();
        var second = factory.CreateClient();
        var cartA = await AddAsync(first, variant, 1);
        var cartB = await AddAsync(second, variant, 1);
        var quoteA = await QuoteAsync(first);
        var quoteB = await QuoteAsync(second);
        var responses = await Task.WhenAll(
            PlaceAsync(first, CatalogHttp.Token(), quoteA.GetProperty("grandTotal").GetString()!, Lines(cartA)),
            PlaceAsync(second, CatalogHttp.Token(), quoteB.GetProperty("grandTotal").GetString()!, Lines(cartB)));
        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Created));
        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(1, await ReservedAsync(variant));
    }

    [Fact]
    public async Task Cancel_and_expiration_each_release_stock_once()
    {
        var admin = await CatalogHttp.AdminClientAsync(factory);
        var variant = await CreateVariantAsync(admin, "Release tee", "400.00", 2);
        var client = factory.CreateClient();
        var cart = await AddAsync(client, variant, 1);
        var quote = await QuoteAsync(client);
        var placed = await ReadAsync(await PlaceAsync(client, CatalogHttp.Token(), quote.GetProperty("grandTotal").GetString()!, Lines(cart)));
        var orderId = placed.GetProperty("order").GetProperty("id").GetGuid();
        var created = placed.GetProperty("order").GetProperty("createdAt").GetDateTimeOffset();
        var expires = placed.GetProperty("order").GetProperty("expiresAt").GetDateTimeOffset();
        Assert.Equal(TimeSpan.FromMinutes(30), expires - created);
        Assert.Contains("30", placed.GetProperty("order").GetProperty("reservationMinutes").GetRawText(), StringComparison.Ordinal);
        var cancel = await CatalogHttp.PostAsync(admin, $"/api/v1/admin/orders/{orderId}/cancel", new { reason = "Customer asked" });
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        var again = await CatalogHttp.PostAsync(admin, $"/api/v1/admin/orders/{orderId}/cancel", new { reason = "Again" });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(0, await ReservedAsync(variant));
        Assert.Equal(1, await ReleaseCountAsync(variant));

        var secondClient = factory.CreateClient();
        var secondCart = await AddAsync(secondClient, variant, 1);
        var secondQuote = await QuoteAsync(secondClient);
        var second = await ReadAsync(await PlaceAsync(secondClient, CatalogHttp.Token(), secondQuote.GetProperty("grandTotal").GetString()!, Lines(secondCart)));
        var secondId = second.GetProperty("order").GetProperty("id").GetGuid();
        await ExpireOrderAsync(secondId);
        using var scope = factory.Services.CreateScope();
        var maintenance = scope.ServiceProvider.GetRequiredService<ICommerceMaintenance>();
        Assert.Equal(1, await maintenance.ExpireDueAsync(CancellationToken.None));
        Assert.Equal(0, await maintenance.ExpireDueAsync(CancellationToken.None));
        Assert.Equal(0, await ReservedAsync(variant));
    }

    [Fact]
    public async Task Cancel_and_expiration_do_not_both_release_the_same_reservation()
    {
        var admin = await CatalogHttp.AdminClientAsync(factory);
        var variant = await CreateVariantAsync(admin, "Overlap tee", "300.00", 1);
        var client = factory.CreateClient();
        var cart = await AddAsync(client, variant, 1);
        var quote = await QuoteAsync(client);
        var placed = await ReadAsync(await PlaceAsync(client, CatalogHttp.Token(), quote.GetProperty("grandTotal").GetString()!, Lines(cart)));
        var orderId = placed.GetProperty("order").GetProperty("id").GetGuid();
        await ExpireOrderAsync(orderId);
        var cancel = CatalogHttp.PostAsync(admin, $"/api/v1/admin/orders/{orderId}/cancel", new { reason = "Overlap" });
        var expire = Task.Run(async () =>
        {
            using var scope = factory.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ICommerceMaintenance>().ExpireDueAsync(CancellationToken.None);
        });
        await Task.WhenAll(cancel, expire);
        Assert.Equal(0, await ReservedAsync(variant));
        Assert.Equal(1, await ReleaseCountAsync(variant));
    }

    [Fact]
    public async Task Guest_access_rejects_reference_wrong_and_expired_tokens()
    {
        var admin = await CatalogHttp.AdminClientAsync(factory);
        var variant = await CreateVariantAsync(admin, "Access tee", "200.00", 2);
        var client = factory.CreateClient();
        var cart = await AddAsync(client, variant, 1);
        var quote = await QuoteAsync(client);
        var placed = await ReadAsync(await PlaceAsync(client, CatalogHttp.Token(), quote.GetProperty("grandTotal").GetString()!, Lines(cart)));
        var reference = placed.GetProperty("order").GetProperty("reference").GetString()!;
        Assert.False(placed.TryGetProperty("accessToken", out _));
        var stranger = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/v1/orders/{reference}")).StatusCode);
        var wrong = new HttpRequestMessage(HttpMethod.Get, "/api/v1/orders/access");
        wrong.Headers.TryAddWithoutValidation("X-Order-Access", "not-the-token");
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.SendAsync(wrong)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync("/api/v1/orders/access")).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
            var stored = await database.OrderAccessTokens.SingleAsync(item => item.OrderId == placed.GetProperty("order").GetProperty("id").GetGuid());
            stored.TokenHash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("expired-capability"));
            stored.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            await database.SaveChangesAsync();
        }

        var expired = new HttpRequestMessage(HttpMethod.Get, "/api/v1/orders/access");
        expired.Headers.TryAddWithoutValidation("X-Order-Access", "expired-capability");
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.SendAsync(expired)).StatusCode);
    }

    [Fact]
    public async Task Another_customer_cannot_read_the_order()
    {
        var admin = await CatalogHttp.AdminClientAsync(factory);
        var variant = await CreateVariantAsync(admin, "Private tee", "150.00", 2);
        var owner = factory.CreateClient();
        await AuthHttp.RegisterAsync(owner, AuthHttp.EmailAddress());
        var cart = await AddAsync(owner, variant, 1);
        var quote = await QuoteAsync(owner);
        var placed = await ReadAsync(await PlaceAsync(owner, CatalogHttp.Token(), quote.GetProperty("grandTotal").GetString()!, Lines(cart)));
        var reference = placed.GetProperty("order").GetProperty("reference").GetString()!;
        var other = factory.CreateClient();
        await AuthHttp.RegisterAsync(other, AuthHttp.EmailAddress());
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/orders/{reference}")).StatusCode);
        var own = await owner.GetAsync($"/api/v1/orders/{reference}");
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
    }

    [Fact]
    public async Task Admin_cancel_requires_csrf_and_snapshots_ignore_later_catalog_edits()
    {
        var admin = await CatalogHttp.AdminClientAsync(factory);
        var variant = await CreateVariantAsync(admin, "Snapshot tee", "120.00", 2);
        var client = factory.CreateClient();
        var cart = await AddAsync(client, variant, 2);
        var quote = await QuoteAsync(client);
        var placed = await ReadAsync(await PlaceAsync(client, CatalogHttp.Token(), quote.GetProperty("grandTotal").GetString()!, Lines(cart)));
        var orderId = placed.GetProperty("order").GetProperty("id").GetGuid();
        Assert.Equal("Snapshot tee", placed.GetProperty("order").GetProperty("items")[0].GetProperty("productName").GetString());
        Assert.False(placed.GetProperty("order").GetProperty("paymentCollected").GetBoolean());
        var renamed = await CatalogHttp.PatchAsync(admin, $"/api/v1/admin/products/{await ProductIdForVariantAsync(variant)}", new { name = "Renamed tee" });
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        admin.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        var blocked = await admin.PostAsJsonAsync($"/api/v1/admin/orders/{orderId}/cancel", new { reason = "No token" });
        Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);
        await AuthHttp.PrepareAsync(admin);
        var detail = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/orders/{orderId}");
        Assert.Equal("Snapshot tee", detail.GetProperty("items")[0].GetProperty("productName").GetString());
        var list = await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/orders");
        Assert.Equal("149.00", list.GetProperty("shippingFee").GetString());
        Assert.Equal("2500.00", list.GetProperty("freeShippingThreshold").GetString());
    }

    [Fact]
    public async Task Outbox_leases_retries_and_keeps_the_order_when_smtp_fails()
    {
        var admin = await CatalogHttp.AdminClientAsync(factory);
        var variant = await CreateVariantAsync(admin, "Mail tee", "90.00", 2);
        var client = factory.CreateClient();
        var cart = await AddAsync(client, variant, 1);
        var quote = await QuoteAsync(client);
        factory.Email.FailuresRemaining = 0;
        using (var warmup = factory.Services.CreateScope())
        {
            var drain = warmup.ServiceProvider.GetRequiredService<ICommerceMaintenance>();
            while (await drain.DispatchOutboxAsync(CancellationToken.None) > 0)
            {
            }
        }

        factory.Email.FailuresRemaining = 1;
        var placed = await ReadAsync(await PlaceAsync(client, CatalogHttp.Token(), quote.GetProperty("grandTotal").GetString()!, Lines(cart)));
        var orderId = placed.GetProperty("order").GetProperty("id").GetGuid();
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var maintenance = scope.ServiceProvider.GetRequiredService<ICommerceMaintenance>();
        Assert.Equal(0, await maintenance.DispatchOutboxAsync(CancellationToken.None));
        Assert.Equal(OrderStatus.PaymentPending, await database.Orders.Where(order => order.Id == orderId).Select(order => order.Status).SingleAsync());
        var failed = await database.EmailOutbox.SingleAsync(message => message.OrderId == orderId);
        Assert.Null(failed.SentAt);
        Assert.Equal(1, failed.Attempts);
        Assert.Equal("smtp-421", failed.LastError);
        Assert.True(failed.NextAttemptAt > DateTimeOffset.UtcNow.AddSeconds(50));
        failed.NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        await database.SaveChangesAsync();
        Assert.Equal(1, await maintenance.DispatchOutboxAsync(CancellationToken.None));
        Assert.Contains("Payment has not been collected", factory.Email.Sent[^1].Text, StringComparison.Ordinal);
        var sentBefore = factory.Email.Sent.Count;
        failed.SentAt = null;
        failed.LockedUntil = DateTimeOffset.UtcNow.AddMinutes(-1);
        failed.NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        await database.SaveChangesAsync();
        Assert.Equal(1, await maintenance.DispatchOutboxAsync(CancellationToken.None));
        Assert.Equal(sentBefore + 1, factory.Email.Sent.Count);
    }

    [Fact]
    public async Task Guest_confirmation_session_survives_refresh_and_rejects_foreign_cookies()
    {
        var admin = await CatalogHttp.AdminClientAsync(factory);
        var firstVariant = await CreateVariantAsync(admin, "Confirm tee", "400.00", 4);
        var secondVariant = await CreateVariantAsync(admin, "Confirm second", "450.00", 4);
        var client = factory.CreateClient();
        var cart = await AddAsync(client, firstVariant, 1);
        var quote = await QuoteAsync(client);
        var placed = await PlaceAsync(client, CatalogHttp.Token(), quote.GetProperty("grandTotal").GetString()!, Lines(cart));
        Assert.Equal(HttpStatusCode.Created, placed.StatusCode);
        Assert.Contains("cide-confirm=", placed.Headers.GetValues("Set-Cookie").First(), StringComparison.Ordinal);
        var again = await AddAsync(client, secondVariant, 1);
        var nextQuote = await QuoteAsync(client);
        var second = await PlaceAsync(client, CatalogHttp.Token(), nextQuote.GetProperty("grandTotal").GetString()!, Lines(again));
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.False(second.Headers.Contains("Set-Cookie") && second.Headers.GetValues("Set-Cookie").Any(value => value.StartsWith("cide-confirm=", StringComparison.Ordinal)));
        var page = await client.GetFromJsonAsync<JsonElement>("/api/v1/orders/confirmation");
        Assert.Equal(2, page.GetProperty("orders").GetArrayLength());
        Assert.Equal(2, page.GetProperty("recoveryHours").GetInt32());
        var otherTab = factory.CreateClient();
        otherTab.DefaultRequestHeaders.Add("Cookie", CookieHeader(placed, "cide-confirm"));
        Assert.Equal(HttpStatusCode.OK, (await otherTab.GetAsync("/api/v1/orders/confirmation")).StatusCode);
        var forged = factory.CreateClient();
        forged.DefaultRequestHeaders.Add("Cookie", "cide-confirm=forged-token");
        Assert.Equal(HttpStatusCode.NotFound, (await forged.GetAsync("/api/v1/orders/confirmation")).StatusCode);

        var planted = factory.CreateClient();
        planted.DefaultRequestHeaders.Add("Cookie", CookieHeader(placed, "cide-confirm"));
        var plantedVariant = await CreateVariantAsync(admin, "Planted tee", "410.00", 2);
        var plantedCart = await AddAsync(planted, plantedVariant, 1);
        var plantedQuote = await QuoteAsync(planted);
        var plantedOrder = await PlaceAsync(planted, CatalogHttp.Token(), plantedQuote.GetProperty("grandTotal").GetString()!, Lines(plantedCart));
        Assert.Equal(HttpStatusCode.Created, plantedOrder.StatusCode);
        var victim = await client.GetFromJsonAsync<JsonElement>("/api/v1/orders/confirmation");
        var victimReferences = victim.GetProperty("orders").EnumerateArray().Select(order => order.GetProperty("reference").GetString()).ToArray();
        var plantedBody = await planted.GetFromJsonAsync<JsonElement>("/api/v1/orders/confirmation");
        var plantedReference = plantedBody.GetProperty("orders")[0].GetProperty("reference").GetString();
        Assert.DoesNotContain(plantedReference, victimReferences);
    }

    [Fact]
    public async Task Expired_confirmation_cookie_is_replaced()
    {
        var admin = await CatalogHttp.AdminClientAsync(factory);
        var variant = await CreateVariantAsync(admin, "Expiry tee", "420.00", 3);
        var client = factory.CreateClient();
        var cart = await AddAsync(client, variant, 1);
        var quote = await QuoteAsync(client);
        var placed = await PlaceAsync(client, CatalogHttp.Token(), quote.GetProperty("grandTotal").GetString()!, Lines(cart));
        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
            var orderId = (await placed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("order").GetProperty("id").GetGuid();
            var link = await database.OrderConfirmationSessionOrders.SingleAsync(item => item.OrderId == orderId);
            var session = await database.OrderConfirmationSessions.SingleAsync(item => item.Id == link.SessionId);
            session.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            await database.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/orders/confirmation")).StatusCode);
        var more = await AddAsync(client, variant, 1);
        var fresh = await QuoteAsync(client);
        var next = await PlaceAsync(client, CatalogHttp.Token(), fresh.GetProperty("grandTotal").GetString()!, Lines(more));
        Assert.Contains("cide-confirm=", next.Headers.GetValues("Set-Cookie").First(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/orders/confirmation")).StatusCode);
    }

    [Fact]
    public async Task Different_idempotency_keys_do_not_create_two_orders()
    {
        var admin = await CatalogHttp.AdminClientAsync(factory);
        var variant = await CreateVariantAsync(admin, "Two key tee", "430.00", 2);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var cart = await AddAsync(client, variant, 1);
        var quote = await QuoteAsync(client);
        var total = quote.GetProperty("grandTotal").GetString()!;
        var lines = Lines(cart);
        await AuthHttp.PrepareAsync(client);
        var responses = await Task.WhenAll(
            PlaceAsync(client, CatalogHttp.Token(), total, lines, prepare: false),
            PlaceAsync(client, CatalogHttp.Token(), total, lines, prepare: false));
        Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(1, await ReservedAsync(variant));
    }

    [Fact]
    public async Task Unpublished_or_repriced_variant_rejects_the_quote()
    {
        var admin = await CatalogHttp.AdminClientAsync(factory);
        var variant = await CreateVariantAsync(admin, "Repriced tee", "440.00", 2);
        var client = factory.CreateClient();
        var cart = await AddAsync(client, variant, 1);
        var quote = await QuoteAsync(client);
        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
            var stored = await database.ProductVariants.SingleAsync(item => item.Id == variant);
            stored.Price = 999m;
            await database.SaveChangesAsync();
        }

        var stale = await PlaceAsync(client, CatalogHttp.Token(), quote.GetProperty("grandTotal").GetString()!, Lines(cart));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(0, await ReservedAsync(variant));
        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
            var product = await database.Products.SingleAsync(item => item.Variants.Any(option => option.Id == variant));
            product.Status = ProductStatus.Archived;
            await database.SaveChangesAsync();
        }

        var hidden = await PlaceAsync(client, CatalogHttp.Token(), "1449.00", Lines(cart));
        Assert.Equal(HttpStatusCode.Conflict, hidden.StatusCode);
        Assert.Equal(0, await ReservedAsync(variant));
    }

    [Fact]
    public async Task Place_order_without_csrf_creates_nothing()
    {
        var admin = await CatalogHttp.AdminClientAsync(factory);
        var variant = await CreateVariantAsync(admin, "Csrf tee", "210.00", 1);
        var client = factory.CreateClient();
        var cart = await AddAsync(client, variant, 1);
        var quote = await QuoteAsync(client);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/orders")
        {
            Content = JsonContent.Create(new
            {
                email = "shopper@example.com",
                phone = "+905550000001",
                lines = Lines(cart),
                shipping = Address(),
                billingSameAsShipping = true,
                expectedGrandTotal = quote.GetProperty("grandTotal").GetString(),
            }),
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", CatalogHttp.Token());
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(request)).StatusCode);
        Assert.Equal(0, await OrdersForVariantAsync(variant));
    }

    private static string CookieHeader(HttpResponseMessage response, string name)
    {
        var value = response.Headers.GetValues("Set-Cookie").First(cookie => cookie.StartsWith(name + "=", StringComparison.Ordinal));
        return value.Split(';', 2)[0];
    }

    private static async Task<JsonElement> AddAsync(HttpClient client, Guid variantId, int quantity)
    {
        var response = await CatalogHttp.PostAsync(client, "/api/v1/cart/items", new { variantId, quantity });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>())!;
    }

    private static async Task<JsonElement> QuoteAsync(HttpClient client)
    {
        var response = await CatalogHttp.PostAsync(client, "/api/v1/checkout/quote", new { });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>())!;
    }

    private static async Task<HttpResponseMessage> PlaceAsync(
        HttpClient client,
        string key,
        string expectedGrandTotal,
        IReadOnlyList<object> lines,
        string phone = "+905550000001",
        bool prepare = true)
    {
        if (prepare)
        {
            await AuthHttp.PrepareAsync(client);
        }
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/orders")
        {
            Content = JsonContent.Create(new
            {
                email = "shopper@example.com",
                phone,
                lines,
                shipping = Address(),
                billingSameAsShipping = true,
                expectedGrandTotal,
            }),
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", key);
        return await client.SendAsync(request);
    }

    private static object Address() => new
    {
        recipient = "Ada Lovelace",
        line1 = "Istiklal 1",
        district = "Beyoglu",
        city = "Istanbul",
        postalCode = "34430",
        country = "TR",
    };

    private static List<object> Lines(JsonElement cart) =>
        cart.GetProperty("items").EnumerateArray().Select(item => (object)new
        {
            variantId = item.GetProperty("variantId").GetGuid(),
            quantity = item.GetProperty("quantity").GetInt32(),
        }).ToList();

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>())!;
    }

    private async Task<Guid> CreateVariantAsync(HttpClient admin, string name, string price, int stock)
    {
        var category = await CatalogHttp.PostAsync(admin, "/api/v1/admin/categories", new
        {
            name = $"{name} cat {Guid.NewGuid():N}"[..24],
            displayOrder = 1,
            isActive = true,
        });
        Assert.Equal(HttpStatusCode.Created, category.StatusCode);
        var categoryId = (await category.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var created = await CatalogHttp.PostAsync(admin, "/api/v1/admin/products", new
        {
            name,
            shortDescription = $"{name} short",
            description = $"{name} description for checkout.",
            categoryIds = new[] { categoryId },
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var productId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var option = await CatalogHttp.PostAsync(admin, $"/api/v1/admin/products/{productId}/options", new { name = "Size" });
        var optionId = (await option.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var value = await CatalogHttp.PostAsync(admin, $"/api/v1/admin/options/{optionId}/values", new { value = "M" });
        var valueId = (await value.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var variant = await CatalogHttp.PostAsync(admin, $"/api/v1/admin/products/{productId}/variants", new
        {
            sku = $"SKU-{Guid.NewGuid():N}"[..20].ToUpperInvariant(),
            price,
            optionValueIds = new[] { valueId },
            initialQuantity = stock,
        });
        Assert.Equal(HttpStatusCode.Created, variant.StatusCode);
        var variantId = (await variant.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var upload = await CatalogHttp.UploadAsync(admin, $"/api/v1/admin/products/{productId}/media", CatalogHttp.Png, $"{name} photo");
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var published = await CatalogHttp.PostAsync(admin, $"/api/v1/admin/products/{productId}/publish", new { });
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        return variantId;
    }

    private async Task<int> ReservedAsync(Guid variantId)
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        return await database.InventoryBalances.Where(balance => balance.VariantId == variantId).Select(balance => balance.Reserved).SingleAsync();
    }

    private async Task<int> SubmissionCountAsync(string key)
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        return await database.OrderSubmissions.CountAsync(submission => submission.IdempotencyKey == key);
    }

    private async Task<int> OrdersForVariantAsync(Guid variantId)
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        return await database.OrderItems.CountAsync(item => item.VariantId == variantId);
    }

    private async Task<Guid> ProductIdForVariantAsync(Guid variantId)
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        return await database.ProductVariants.Where(variant => variant.Id == variantId).Select(variant => variant.ProductId).SingleAsync();
    }

    private async Task<int> ReleaseCountAsync(Guid variantId)
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        return await database.InventoryMovements.CountAsync(movement =>
            movement.VariantId == variantId && movement.MovementType == InventoryMovementType.ReservationRelease);
    }

    private async Task AssertTokenStoredOnlyAsHashAsync(string rawToken)
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var payloads = await database.EmailOutbox.Select(message => message.Payload).ToListAsync();
        Assert.All(payloads, payload => Assert.DoesNotContain(rawToken, payload, StringComparison.Ordinal));
        var hashes = await database.OrderAccessTokens.Select(token => token.TokenHash).ToListAsync();
        Assert.Contains(hashes, hash => hash.SequenceEqual(SecureTokensHash(rawToken)));
    }

    private static byte[] SecureTokensHash(string raw) =>
        System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw));

    private async Task ExpireOrderAsync(Guid orderId)
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var order = await database.Orders.SingleAsync(item => item.Id == orderId);
        order.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await database.SaveChangesAsync();
    }

}
