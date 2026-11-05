using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;

namespace Commerce.Api.Cors;

public sealed class FrontendCorsOptions
{
    public const string SectionName = "Cors";

    /// <summary>Exact storefront and admin origins, for example http://localhost:5173.</summary>
    public string[] AllowedOrigins { get; set; } = [];
}

public static class CorsSetup
{
    public const string PolicyName = "Frontends";

    /// <summary>
    /// Explicit-origin CORS with credentials enabled, ready for HttpOnly cookie authentication.
    /// Wildcards are rejected at startup.
    /// </summary>
    public static IServiceCollection AddFrontendCors(this IServiceCollection services)
    {
        services.AddOptions<FrontendCorsOptions>()
            .BindConfiguration(FrontendCorsOptions.SectionName)
            .Validate(
                options => options.AllowedOrigins.All(IsExplicitOrigin),
                "Cors:AllowedOrigins must contain absolute http(s) origins without wildcards, paths or trailing slashes.")
            .ValidateOnStart();

        services.AddCors();
        services.AddOptions<CorsOptions>()
            .Configure<IOptions<FrontendCorsOptions>>((cors, frontend) =>
                cors.AddPolicy(PolicyName, policy => policy
                    .WithOrigins(frontend.Value.AllowedOrigins)
                    .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
                    .AllowAnyHeader()
                    .AllowCredentials()
                    .SetPreflightMaxAge(TimeSpan.FromMinutes(10))));

        return services;
    }

    private static bool IsExplicitOrigin(string origin) =>
        Uri.TryCreate(origin, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && !origin.Contains('*')
        && uri.GetLeftPart(UriPartial.Authority) == origin;
}
