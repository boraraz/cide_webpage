using System.Threading.RateLimiting;
using Commerce.Infrastructure.Catalog;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Commerce.Api.Catalog;

internal static class CatalogRateLimits
{
    public const string MediaUpload = "catalog-media-upload";

    public static IServiceCollection AddCatalogRateLimits(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(MediaUpload, http =>
            {
                var limits = http.RequestServices.GetRequiredService<IOptions<CatalogOptions>>().Value;
                var key = $"{MediaUpload}:{http.Connection.RemoteIpAddress}";
                return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = Math.Max(1, limits.MediaUploadPermitLimit),
                    Window = TimeSpan.FromMinutes(Math.Max(1, limits.MediaUploadWindowMinutes)),
                    QueueLimit = 0,
                });
            });
        });

        return services;
    }
}
