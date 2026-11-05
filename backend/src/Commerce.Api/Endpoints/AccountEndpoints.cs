using System.Security.Claims;
using Commerce.Api.Auth;
using Commerce.Application.Identity;

namespace Commerce.Api.Endpoints;

public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/account")
            .WithTags("Account")
            .RequireAuthorization();

        group.MapGet("/profile", GetProfileAsync)
            .WithName("GetProfile")
            .Produces<UserResponse>();

        group.MapPatch("/profile", UpdateProfileAsync)
            .WithName("UpdateProfile")
            .AddEndpointFilter<RequireAntiforgery>()
            .Produces<UserResponse>()
            .ProducesValidationProblem();

        group.MapPost("/change-password", ChangePasswordAsync)
            .WithName("ChangePassword")
            .WithSummary("Changes the password and revokes every existing session.")
            .AddEndpointFilter<RequireAntiforgery>()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem();

        return app;
    }

    private static async Task<IResult> GetProfileAsync(
        ClaimsPrincipal principal,
        IdentityService identity,
        CancellationToken cancellationToken)
    {
        if (IdentityHttp.UserId(principal) is not Guid userId)
        {
            return Results.Unauthorized();
        }

        var user = await identity.GetUserAsync(userId, cancellationToken);
        return user is null ? Results.Unauthorized() : TypedResults.Ok(IdentityHttp.ToResponse(user));
    }

    private static async Task<IResult> UpdateProfileAsync(
        UpdateProfileRequest request,
        ClaimsPrincipal principal,
        IdentityService identity,
        CancellationToken cancellationToken)
    {
        if (IdentityHttp.UserId(principal) is not Guid userId)
        {
            return Results.Unauthorized();
        }

        var result = await identity.UpdateProfileAsync(
            userId, request.FirstName, request.LastName, request.PhoneNumber, cancellationToken);
        return result.Succeeded
            ? TypedResults.Ok(IdentityHttp.ToResponse(result.Value!))
            : IdentityHttp.Problem(result.Failure!);
    }

    private static async Task<IResult> ChangePasswordAsync(
        ChangePasswordRequest request,
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

        var result = await identity.ChangePasswordAsync(
            userId, request.CurrentPassword, request.NewPassword, request.PasswordConfirmation, cancellationToken);
        if (!result.Succeeded)
        {
            return IdentityHttp.Problem(result.Failure!);
        }

        SessionCookies.Delete(http.Response, environment);
        return TypedResults.NoContent();
    }
}
