using System.Security.Claims;
using System.Text.Encodings.Web;
using Commerce.Application.Identity;
using Commerce.Domain.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Commerce.Api.Auth;

internal sealed class SessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IdentityService identity,
    IHostEnvironment environment)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Session";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = SessionCookies.Read(Request, environment);
        if (string.IsNullOrEmpty(token))
        {
            return AuthenticateResult.NoResult();
        }

        var resolved = await identity.ResolveSessionAsync(token, Context.RequestAborted);
        if (resolved is null)
        {
            return AuthenticateResult.NoResult();
        }

        if (resolved.RotatedToken is not null && resolved.RotatedExpiresAt is not null)
        {
            SessionCookies.Append(Response, environment, resolved.RotatedToken, resolved.RotatedExpiresAt.Value);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, resolved.User.Id.ToString()),
            new(ClaimTypes.Email, resolved.User.Email),
            new(ClaimTypes.Name, resolved.User.Email),
        };
        claims.AddRange(resolved.User.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }
}

internal static class SessionAuthenticationSetup
{
    public static IServiceCollection AddSessionAuthentication(this IServiceCollection services, IHostEnvironment environment)
    {
        services.AddAuthentication(SessionAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(SessionAuthenticationHandler.SchemeName, _ => { });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(RoleNames.Admin, policy => policy.RequireRole(RoleNames.Admin));
        });

        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.Name = "cide-csrf";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.Path = "/";
            options.Cookie.SecurePolicy = environment.IsDevelopment() || environment.IsEnvironment("Testing")
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
        });

        return services;
    }
}
