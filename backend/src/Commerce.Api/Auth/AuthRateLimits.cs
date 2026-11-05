using System.Threading.RateLimiting;
using Commerce.Infrastructure.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Commerce.Api.Auth;

internal static class AuthRateLimits
{
    public const string Register = "auth-register";
    public const string Login = "auth-login";
    public const string Verification = "auth-verification";
    public const string PasswordReset = "auth-password-reset";

    public static IServiceCollection AddAuthRateLimits(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, _) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await Results.Problem(
                    title: "Too many requests. Try again later.",
                    statusCode: StatusCodes.Status429TooManyRequests).ExecuteAsync(context.HttpContext);
            };

            options.AddPolicy(Register, http => Window(http, Register, limits => limits.RegisterPermitLimit, limits => limits.RegisterWindowMinutes));
            options.AddPolicy(Login, http => Window(http, Login, limits => limits.LoginPermitLimit, limits => limits.LoginWindowMinutes));
            options.AddPolicy(Verification, http => Window(http, Verification, limits => limits.VerificationPermitLimit, limits => limits.VerificationWindowMinutes));
            options.AddPolicy(PasswordReset, http => Window(http, PasswordReset, limits => limits.PasswordResetPermitLimit, limits => limits.PasswordResetWindowMinutes));
        });

        return services;
    }

    private static RateLimitPartition<string> Window(
        HttpContext http,
        string policy,
        Func<AuthLimitOptions, int> permit,
        Func<AuthLimitOptions, int> minutes)
    {
        var limits = http.RequestServices.GetRequiredService<IOptions<AuthLimitOptions>>().Value;
        var key = $"{policy}:{http.Connection.RemoteIpAddress}";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = Math.Max(1, permit(limits)),
            Window = TimeSpan.FromMinutes(Math.Max(1, minutes(limits))),
            QueueLimit = 0,
        });
    }
}
