using System.Threading.RateLimiting;
using Commerce.Application.Commerce;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Commerce.Api.Auth;

internal static class CommerceRateLimits
{
    public const string CartWrite = "commerce-cart-write";
    public const string Quote = "commerce-quote";
    public const string Submit = "commerce-submit";
    public const string GuestLookup = "commerce-guest-lookup";

    public static IServiceCollection AddCommerceRateLimits(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(CartWrite, http => Window(http, CartWrite, limits => limits.CartWritePermitLimit));
            options.AddPolicy(Quote, http => Window(http, Quote, limits => limits.QuotePermitLimit));
            options.AddPolicy(Submit, http => Window(http, Submit, limits => limits.SubmitPermitLimit));
            options.AddPolicy(GuestLookup, http => Window(http, GuestLookup, limits => limits.GuestLookupPermitLimit));
        });
        return services;
    }

    private static RateLimitPartition<string> Window(HttpContext http, string policy, Func<CommerceOptions, int> permit)
    {
        var limits = http.RequestServices.GetRequiredService<IOptions<CommerceOptions>>().Value;
        var key = $"{policy}:{http.Connection.RemoteIpAddress}";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = Math.Max(1, permit(limits)),
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        });
    }
}
