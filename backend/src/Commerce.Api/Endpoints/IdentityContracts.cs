using System.Security.Claims;
using Commerce.Application.Identity;

namespace Commerce.Api.Endpoints;

public sealed record CsrfResponse(string Token);

public sealed record UserResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    bool EmailVerified,
    IReadOnlyList<string> Roles);

public sealed record RegisterRequest(
    string? FirstName,
    string? LastName,
    string? Email,
    string? Password,
    string? PasswordConfirmation);

public sealed record LoginRequest(string? Email, string? Password);

public sealed record EmailOnlyRequest(string? Email);

public sealed record ConfirmTokenRequest(string? Token);

public sealed record ResetPasswordRequest(string? Token, string? Password, string? PasswordConfirmation);

public sealed record UpdateProfileRequest(string? FirstName, string? LastName, string? PhoneNumber);

public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword, string? PasswordConfirmation);

internal static class IdentityHttp
{
    public static IResult Problem(IdentityFailure failure) =>
        failure.Errors is null
            ? Results.Problem(title: failure.Title, detail: failure.Detail, statusCode: failure.Status)
            : Results.ValidationProblem(failure.Errors, title: failure.Title, statusCode: failure.Status);

    public static UserResponse ToResponse(CurrentUser user) => new(
        user.Id,
        user.Email,
        user.FirstName,
        user.LastName,
        user.PhoneNumber,
        user.EmailVerified,
        user.Roles);

    public static ClientInfo Client(HttpContext http) => new(
        http.Connection.RemoteIpAddress?.ToString(),
        http.Request.Headers.UserAgent.ToString());

    public static Guid? UserId(ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id) ? id : null;
    }
}
