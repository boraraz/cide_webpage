using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace Commerce.Api.Security;

public static class SecurityHeaders
{
    public static IServiceCollection AddEdgeSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            ApplyForwarding(options, configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? []);
        });
        return services;
    }

    public static void ApplyForwarding(ForwardedHeadersOptions options, IReadOnlyList<string> proxies)
    {
        options.ForwardLimit = 1;
        options.KnownProxies.Clear();
#pragma warning disable ASPDEPR005
        options.KnownNetworks.Clear();
#pragma warning restore ASPDEPR005
        options.KnownIPNetworks.Clear();
        foreach (var proxy in proxies)
        {
            if (IPAddress.TryParse(proxy, out var address))
            {
                options.KnownProxies.Add(address);
            }
        }

        // An empty allow-list makes this middleware trust every peer. Leave forwarding off
        // until the operator names the proxy.
        options.ForwardedHeaders = options.KnownProxies.Count == 0
            ? ForwardedHeaders.None
            : ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    }

    public static IApplicationBuilder UseApiSecurityHeaders(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'";
            await next(context);
        });
    }
}
