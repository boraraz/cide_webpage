using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Commerce.Infrastructure.Identity;

public sealed class ProductionSettingsCheck(IHostEnvironment environment, IConfiguration configuration) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!ProductionSettings.IsAcceptable(environment, configuration, out var error))
        {
            throw new InvalidOperationException(error);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public static class ProductionSettings
{
    public static bool IsAcceptable(IHostEnvironment environment, IConfiguration configuration, out string error)
    {
        if (!environment.IsProduction())
        {
            error = string.Empty;
            return true;
        }

        var problems = new List<string>();
        var hosts = configuration["AllowedHosts"];
        if (string.IsNullOrWhiteSpace(hosts) || hosts is "*" or "localhost")
        {
            problems.Add("AllowedHosts must name the production site and must not be localhost or *.");
        }

        var storefront = configuration["PublicUrls:StorefrontUrl"];
        if (!Uri.TryCreate(storefront, UriKind.Absolute, out var storefrontUri) || storefrontUri.Scheme != Uri.UriSchemeHttps)
        {
            problems.Add("PublicUrls:StorefrontUrl must be an absolute https URL.");
        }

        if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("Database")))
        {
            problems.Add("ConnectionStrings:Database is required.");
        }

        if (string.IsNullOrWhiteSpace(configuration["ObjectStorage:AccessKey"])
            || string.IsNullOrWhiteSpace(configuration["ObjectStorage:SecretKey"])
            || string.IsNullOrWhiteSpace(configuration["ObjectStorage:BucketName"])
            || string.IsNullOrWhiteSpace(configuration["ObjectStorage:ServiceUrl"]))
        {
            problems.Add("ObjectStorage access key, secret, bucket, and service URL are required.");
        }

        if (string.IsNullOrWhiteSpace(configuration["Smtp:Host"]) || configuration["Smtp:Host"] == "localhost")
        {
            problems.Add("Smtp:Host must be the production mail host.");
        }

        if (!decimal.TryParse(configuration["Commerce:ShippingFee"], out _)
            || !decimal.TryParse(configuration["Commerce:FreeShippingThreshold"], out _))
        {
            problems.Add("Commerce:ShippingFee and Commerce:FreeShippingThreshold are required.");
        }

        if (bool.TryParse(configuration["Identity:BootstrapAdmin"], out var bootstrap) && bootstrap)
        {
            problems.Add("Identity:BootstrapAdmin must be false.");
        }

        var mode = configuration["PublicUrls:Mode"] ?? "SameOrigin";
        if (mode == "SplitHosts")
        {
            var admin = configuration["PublicUrls:AdminUrl"];
            var api = configuration["PublicUrls:ApiPublicUrl"];
            if (!SameSite(storefront, admin) || !SameSite(storefront, api))
            {
                problems.Add("Split hostnames must share one registrable domain.");
            }

            var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
            if (storefront is not null && !origins.Contains(storefront.TrimEnd('/'), StringComparer.Ordinal))
            {
                problems.Add("Cors:AllowedOrigins must include the storefront origin.");
            }
        }
        else if (mode != "SameOrigin")
        {
            problems.Add("PublicUrls:Mode must be SameOrigin or SplitHosts.");
        }

        error = string.Join(' ', problems);
        return problems.Count == 0;
    }

    private static bool SameSite(string? left, string? right)
    {
        if (!Uri.TryCreate(left, UriKind.Absolute, out var leftUri) || !Uri.TryCreate(right, UriKind.Absolute, out var rightUri))
        {
            return false;
        }

        return Site(leftUri.Host) == Site(rightUri.Host);
    }

    private static string Site(string host)
    {
        if (IPAddress.TryParse(host, out _))
        {
            return host;
        }

        var parts = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length <= 2 ? host : string.Join('.', parts[^2], parts[^1]);
    }
}
