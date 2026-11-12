using System.Security.Claims;
using Commerce.Api.Auth;
using Commerce.Application.Commerce;
using Commerce.Application.Identity;
using Commerce.Domain.Identity;
using Microsoft.AspNetCore.RateLimiting;

namespace Commerce.Api.Endpoints;

public static class AdminAuthEndpoints
{
    public static IEndpointRouteBuilder MapAdminAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/auth").WithTags("Admin authentication");

        group.MapPost("/login", (LoginRequest request, IdentityService identity, CommerceService commerce, HttpContext http, IHostEnvironment environment, CancellationToken cancellationToken) =>
                AuthEndpoints.LoginAsync(request, RoleNames.Admin, identity, commerce, http, environment, cancellationToken))
            .WithName("AdminLogin")
            .WithSummary("Signs in an administrator. Customer accounts receive the same response as a wrong password.")
            .RequireRateLimiting(AuthRateLimits.Login)
            .AddEndpointFilter<RequireAntiforgery>()
            .Produces<UserResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/me", async (ClaimsPrincipal principal, IdentityService identity, CancellationToken cancellationToken) =>
            {
                if (IdentityHttp.UserId(principal) is not Guid userId)
                {
                    return Results.Unauthorized();
                }

                var user = await identity.GetUserAsync(userId, cancellationToken);
                return user is null ? Results.Unauthorized() : Results.Ok(IdentityHttp.ToResponse(user));
            })
            .WithName("GetCurrentAdministrator")
            .RequireAuthorization(RoleNames.Admin)
            .Produces<UserResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return app;
    }
}
