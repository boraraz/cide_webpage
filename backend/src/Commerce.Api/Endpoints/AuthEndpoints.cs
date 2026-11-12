using System.Security.Claims;
using Commerce.Api.Auth;
using Commerce.Application.Commerce;
using Commerce.Application.Identity;
using Commerce.Domain.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.RateLimiting;

namespace Commerce.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth").WithTags("Authentication");

        group.MapGet("/csrf", (IAntiforgery antiforgery, HttpContext http) =>
            {
                var tokens = antiforgery.GetAndStoreTokens(http);
                return TypedResults.Ok(new CsrfResponse(tokens.RequestToken ?? string.Empty));
            })
            .WithName("GetCsrfToken")
            .WithSummary("Issues the CSRF token required on later state-changing requests.")
            .AllowAnonymous();

        group.MapPost("/register", RegisterAsync)
            .WithName("Register")
            .WithSummary("Creates a customer account and signs that customer in.")
            .RequireRateLimiting(AuthRateLimits.Register)
            .AddEndpointFilter<RequireAntiforgery>()
            .Produces<UserResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/login", (LoginRequest request, IdentityService identity, CommerceService commerce, HttpContext http, IHostEnvironment environment, CancellationToken cancellationToken) =>
                LoginAsync(request, RoleNames.Customer, identity, commerce, http, environment, cancellationToken))
            .WithName("Login")
            .WithSummary("Signs in a customer.")
            .RequireRateLimiting(AuthRateLimits.Login)
            .AddEndpointFilter<RequireAntiforgery>()
            .Produces<UserResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/logout", LogoutAsync)
            .WithName("Logout")
            .WithSummary("Revokes the current session.")
            .AddEndpointFilter<RequireAntiforgery>()
            .Produces(StatusCodes.Status204NoContent);

        group.MapPost("/logout-all", LogoutAllAsync)
            .WithName("LogoutAll")
            .WithSummary("Revokes every session for the signed-in user.")
            .RequireAuthorization()
            .AddEndpointFilter<RequireAntiforgery>()
            .Produces(StatusCodes.Status204NoContent);

        group.MapGet("/me", MeAsync)
            .WithName("GetCurrentUser")
            .WithSummary("Returns the signed-in user.")
            .RequireAuthorization()
            .Produces<UserResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/email-verification/request", RequestVerificationAsync)
            .WithName("RequestEmailVerification")
            .WithSummary("Sends a verification email when the account exists. The response is always the same.")
            .RequireRateLimiting(AuthRateLimits.Verification)
            .AddEndpointFilter<RequireAntiforgery>()
            .Produces(StatusCodes.Status202Accepted);

        group.MapPost("/email-verification/confirm", ConfirmVerificationAsync)
            .WithName("ConfirmEmailVerification")
            .AddEndpointFilter<RequireAntiforgery>()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/password-reset/request", RequestResetAsync)
            .WithName("RequestPasswordReset")
            .WithSummary("Sends a reset email when the account exists. The response does not reveal that.")
            .RequireRateLimiting(AuthRateLimits.PasswordReset)
            .AddEndpointFilter<RequireAntiforgery>()
            .Produces(StatusCodes.Status202Accepted);

        group.MapPost("/password-reset/confirm", ConfirmResetAsync)
            .WithName("ConfirmPasswordReset")
            .AddEndpointFilter<RequireAntiforgery>()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        IdentityService identity,
        CommerceService commerce,
        HttpContext http,
        IHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        var result = await identity.RegisterAsync(
            request.FirstName,
            request.LastName,
            request.Email,
            request.Password,
            request.PasswordConfirmation,
            IdentityHttp.Client(http),
            cancellationToken);
        return await SignIn(result, commerce, http, environment, cancellationToken);
    }

    internal static async Task<IResult> LoginAsync(
        LoginRequest request,
        string role,
        IdentityService identity,
        CommerceService commerce,
        HttpContext http,
        IHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        var result = await identity.LoginAsync(
            request.Email,
            request.Password,
            role,
            IdentityHttp.Client(http),
            cancellationToken);
        if (!result.Succeeded)
        {
            return IdentityHttp.Problem(result.Failure!);
        }

        SessionCookies.Append(http.Response, environment, result.Value!.SessionToken, result.Value.SessionExpiresAt);
        if (role == RoleNames.Customer)
        {
            await commerce.MergeAsync(CartCookies.Read(http.Request, environment), result.Value.User.Id, cancellationToken);
        }

        return TypedResults.Ok(IdentityHttp.ToResponse(result.Value.User));
    }

    private static async Task<IResult> LogoutAsync(
        IdentityService identity,
        HttpContext http,
        IHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        await identity.LogoutAsync(SessionCookies.Read(http.Request, environment), cancellationToken);
        SessionCookies.Delete(http.Response, environment);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> LogoutAllAsync(
        ClaimsPrincipal principal,
        IdentityService identity,
        HttpContext http,
        IHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        if (IdentityHttp.UserId(principal) is not Guid userId)
        {
            return Results.Unauthorized();
        }

        await identity.LogoutAllAsync(userId, cancellationToken);
        SessionCookies.Delete(http.Response, environment);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> MeAsync(ClaimsPrincipal principal, IdentityService identity, CancellationToken cancellationToken)
    {
        if (IdentityHttp.UserId(principal) is not Guid userId)
        {
            return Results.Unauthorized();
        }

        var user = await identity.GetUserAsync(userId, cancellationToken);
        return user is null ? Results.Unauthorized() : TypedResults.Ok(IdentityHttp.ToResponse(user));
    }

    private static async Task<IResult> RequestVerificationAsync(
        EmailOnlyRequest request,
        ClaimsPrincipal principal,
        IdentityService identity,
        CancellationToken cancellationToken)
    {
        var current = principal.Identity?.IsAuthenticated == true ? IdentityHttp.UserId(principal) : null;
        await identity.RequestEmailVerificationAsync(request.Email, current, cancellationToken);
        return Results.Accepted();
    }

    private static async Task<IResult> ConfirmVerificationAsync(
        ConfirmTokenRequest request,
        IdentityService identity,
        CancellationToken cancellationToken)
    {
        var result = await identity.ConfirmEmailVerificationAsync(request.Token, cancellationToken);
        return result.Succeeded ? TypedResults.NoContent() : IdentityHttp.Problem(result.Failure!);
    }

    private static async Task<IResult> RequestResetAsync(
        EmailOnlyRequest request,
        IdentityService identity,
        CancellationToken cancellationToken)
    {
        await identity.RequestPasswordResetAsync(request.Email, cancellationToken);
        return Results.Accepted();
    }

    private static async Task<IResult> ConfirmResetAsync(
        ResetPasswordRequest request,
        IdentityService identity,
        CancellationToken cancellationToken)
    {
        var result = await identity.ConfirmPasswordResetAsync(
            request.Token, request.Password, request.PasswordConfirmation, cancellationToken);
        return result.Succeeded ? TypedResults.NoContent() : IdentityHttp.Problem(result.Failure!);
    }

    private static async Task<IResult> SignIn(
        IdentityResult<AuthSuccess> result,
        CommerceService commerce,
        HttpContext http,
        IHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        if (!result.Succeeded)
        {
            return IdentityHttp.Problem(result.Failure!);
        }

        SessionCookies.Append(http.Response, environment, result.Value!.SessionToken, result.Value.SessionExpiresAt);
        await commerce.MergeAsync(CartCookies.Read(http.Request, environment), result.Value.User.Id, cancellationToken);
        return TypedResults.Created("/api/v1/auth/me", IdentityHttp.ToResponse(result.Value.User));
    }
}
