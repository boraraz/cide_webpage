using System.Security.Claims;
using Commerce.Api.Auth;
using Commerce.Application.Commerce;
using Commerce.Application.Identity;
using Microsoft.AspNetCore.RateLimiting;

namespace Commerce.Api.Endpoints;

public static class CommerceEndpoints
{
    public static IEndpointRouteBuilder MapCommerceEndpoints(this IEndpointRouteBuilder app)
    {
        var cart = app.MapGroup("/api/v1/cart").WithTags("Cart");
        cart.MapGet("/", GetCartAsync).WithName("GetCart").AllowAnonymous();
        cart.MapPost("/items", AddItemAsync)
            .WithName("AddCartItem")
            .RequireRateLimiting(CommerceRateLimits.CartWrite)
            .AddEndpointFilter<RequireAntiforgery>();
        cart.MapPatch("/items/{itemId:guid}", UpdateItemAsync)
            .WithName("UpdateCartItem")
            .RequireRateLimiting(CommerceRateLimits.CartWrite)
            .AddEndpointFilter<RequireAntiforgery>();
        cart.MapDelete("/items/{itemId:guid}", RemoveItemAsync)
            .WithName("RemoveCartItem")
            .RequireRateLimiting(CommerceRateLimits.CartWrite)
            .AddEndpointFilter<RequireAntiforgery>();

        app.MapPost("/api/v1/checkout/quote", QuoteAsync)
            .WithTags("Checkout")
            .WithName("QuoteCheckout")
            .RequireRateLimiting(CommerceRateLimits.Quote)
            .AddEndpointFilter<RequireAntiforgery>();

        var orders = app.MapGroup("/api/v1/orders").WithTags("Orders");
        orders.MapPost("/", PlaceOrderAsync)
            .WithName("PlaceOrder")
            .RequireRateLimiting(CommerceRateLimits.Submit)
            .AddEndpointFilter<RequireAntiforgery>();
        orders.MapGet("/", ListOrdersAsync).WithName("ListOrders").RequireAuthorization();
        orders.MapGet("/access", GetOrderByAccessAsync)
            .WithName("GetOrderByAccess")
            .RequireRateLimiting(CommerceRateLimits.GuestLookup)
            .AddEndpointFilter(async (context, next) =>
            {
                context.HttpContext.Response.Headers["Referrer-Policy"] = "no-referrer";
                return await next(context);
            });
        orders.MapGet("/{reference}", GetOrderAsync).WithName("GetOrder").AllowAnonymous();

        var admin = app.MapGroup("/api/v1/admin/orders").WithTags("Admin orders").RequireAuthorization(Commerce.Domain.Identity.RoleNames.Admin);
        admin.MapGet("/", ListAdminOrdersAsync).WithName("ListAdminOrders");
        admin.MapGet("/{orderId:guid}", GetAdminOrderAsync).WithName("GetAdminOrder");
        admin.MapPost("/{orderId:guid}/cancel", CancelOrderAsync)
            .WithName("CancelOrder")
            .AddEndpointFilter<RequireAntiforgery>();
        return app;
    }

    private static async Task<IResult> GetCartAsync(
        ClaimsPrincipal principal, CommerceService commerce, HttpContext http, IHostEnvironment environment, CancellationToken cancellationToken) =>
        Results.Ok(await commerce.GetCartAsync(Customer(principal), Guest(http, environment), cancellationToken));

    private static async Task<IResult> AddItemAsync(
        AddCartItemRequest request,
        ClaimsPrincipal principal,
        CommerceService commerce,
        HttpContext http,
        IHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        var result = await commerce.AddItemAsync(Customer(principal), Guest(http, environment), request.VariantId, request.Quantity, cancellationToken);
        return CartResult(result, http, environment);
    }

    private static async Task<IResult> UpdateItemAsync(
        Guid itemId,
        UpdateCartItemRequest request,
        ClaimsPrincipal principal,
        CommerceService commerce,
        HttpContext http,
        IHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        var result = await commerce.UpdateItemAsync(Customer(principal), Guest(http, environment), itemId, request.Quantity, cancellationToken);
        return From(result, cart =>
        {
            RememberCart(http, environment, null, cart.ExpiresAt);
            return Results.Ok(cart);
        });
    }

    private static async Task<IResult> RemoveItemAsync(
        Guid itemId,
        ClaimsPrincipal principal,
        CommerceService commerce,
        HttpContext http,
        IHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        var result = await commerce.RemoveItemAsync(Customer(principal), Guest(http, environment), itemId, cancellationToken);
        return From(result, cart =>
        {
            RememberCart(http, environment, null, cart.ExpiresAt);
            return Results.Ok(cart);
        });
    }

    private static async Task<IResult> QuoteAsync(
        ClaimsPrincipal principal, CommerceService commerce, HttpContext http, IHostEnvironment environment, CancellationToken cancellationToken)
    {
        var result = await commerce.QuoteAsync(Customer(principal), Guest(http, environment), cancellationToken);
        return From(result, quote => Results.Ok(quote));
    }

    private static async Task<IResult> PlaceOrderAsync(
        PlaceOrderRequest request,
        ClaimsPrincipal principal,
        CommerceService commerce,
        HttpContext http,
        IHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        var lines = (request.Lines ?? []).Select(line => new LineIntent(line.VariantId, line.Quantity)).ToArray();
        var result = await commerce.SubmitAsync(
            Customer(principal),
            Guest(http, environment),
            http.Request.Headers["Idempotency-Key"].FirstOrDefault(),
            request.Email,
            request.Phone,
            lines,
            MapAddress(request.Shipping),
            request.BillingSameAsShipping,
            request.Billing is null ? null : MapAddress(request.Billing),
            request.ExpectedGrandTotal,
            cancellationToken);
        return From(result, placed => Results.Json(placed, statusCode: StatusCodes.Status201Created));
    }

    private static async Task<IResult> ListOrdersAsync(ClaimsPrincipal principal, CommerceService commerce, CancellationToken cancellationToken)
    {
        if (Customer(principal) is not Guid customerId)
        {
            return Results.Unauthorized();
        }

        return Results.Ok(await commerce.ListForCustomerAsync(customerId, cancellationToken));
    }

    private static async Task<IResult> GetOrderAsync(
        string reference, ClaimsPrincipal principal, CommerceService commerce, CancellationToken cancellationToken)
    {
        if (Customer(principal) is not Guid customerId)
        {
            return Results.NotFound();
        }

        var order = await commerce.FindForCustomerAsync(customerId, reference, cancellationToken);
        return order is null ? Results.NotFound() : Results.Ok(order);
    }

    private static async Task<IResult> GetOrderByAccessAsync(HttpContext http, CommerceService commerce, CancellationToken cancellationToken)
    {
        if (!http.Request.Headers.TryGetValue(CartCookies.AccessHeader, out var token))
        {
            return Results.NotFound();
        }

        var order = await commerce.FindByAccessTokenAsync(token.FirstOrDefault(), cancellationToken);
        return order is null ? Results.NotFound() : Results.Ok(order);
    }

    private static async Task<IResult> ListAdminOrdersAsync(
        string? status, string? reference, int? page, int? pageSize, CommerceService commerce, CancellationToken cancellationToken)
    {
        var result = await commerce.ListAdminAsync(status, reference, page ?? 1, pageSize ?? 20, cancellationToken);
        return From(result, list => Results.Ok(list));
    }

    private static async Task<IResult> GetAdminOrderAsync(Guid orderId, CommerceService commerce, CancellationToken cancellationToken)
    {
        var order = await commerce.FindAdminAsync(orderId, cancellationToken);
        return order is null ? Results.NotFound() : Results.Ok(order);
    }

    private static async Task<IResult> CancelOrderAsync(
        Guid orderId, CancelOrderRequest request, ClaimsPrincipal principal, CommerceService commerce, CancellationToken cancellationToken)
    {
        if (IdentityHttp.UserId(principal) is not Guid actorId)
        {
            return Results.Unauthorized();
        }

        var result = await commerce.CancelAsync(orderId, actorId, request.Reason, cancellationToken);
        return From(result, order => Results.Ok(order));
    }

    private static IResult CartResult(CommerceResult<CartChange> result, HttpContext http, IHostEnvironment environment) =>
        From(result, change =>
        {
            RememberCart(http, environment, change.IssuedToken, change.TokenExpiresAt);
            return Results.Ok(change.Cart);
        });

    private static void RememberCart(HttpContext http, IHostEnvironment environment, string? issued, DateTimeOffset? expires)
    {
        if (expires is not DateTimeOffset cookieExpires)
        {
            return;
        }

        if (Customer(http.User) is not null && issued is null)
        {
            return;
        }

        var token = issued ?? CartCookies.Read(http.Request, environment);
        if (!string.IsNullOrWhiteSpace(token))
        {
            CartCookies.Append(http.Response, environment, token, cookieExpires);
        }
    }

    private static Guid? Customer(ClaimsPrincipal principal) =>
        principal.Identity?.IsAuthenticated == true ? IdentityHttp.UserId(principal) : null;

    private static byte[]? Guest(HttpContext http, IHostEnvironment environment)
    {
        var token = CartCookies.Read(http.Request, environment);
        return string.IsNullOrWhiteSpace(token) ? null : SecureTokens.Hash(token);
    }

    private static PostalAddress MapAddress(AddressRequest request) => new(
        request.Recipient,
        request.Line1,
        request.Line2,
        request.District,
        request.City,
        request.PostalCode,
        request.Country,
        request.Notes);

    private static IResult From<T>(CommerceResult<T> result, Func<T, IResult> success)
    {
        if (result.Failure is null)
        {
            return success(result.Value!);
        }

        return result.Failure.Errors is null
            ? Results.Problem(title: result.Failure.Title, detail: result.Failure.Detail, statusCode: result.Failure.Status)
            : Results.ValidationProblem(result.Failure.Errors, title: result.Failure.Title, statusCode: result.Failure.Status);
    }
}

internal sealed record AddCartItemRequest(Guid VariantId, int Quantity);

internal sealed record UpdateCartItemRequest(int Quantity);

internal sealed record AddressRequest(
    string Recipient,
    string Line1,
    string? Line2,
    string District,
    string City,
    string PostalCode,
    string Country,
    string? Notes);

internal sealed record CheckoutLineRequest(Guid VariantId, int Quantity);

internal sealed record PlaceOrderRequest(
    string Email,
    string Phone,
    IReadOnlyList<CheckoutLineRequest> Lines,
    AddressRequest Shipping,
    bool BillingSameAsShipping,
    AddressRequest? Billing,
    string ExpectedGrandTotal);

internal sealed record CancelOrderRequest(string? Reason);
