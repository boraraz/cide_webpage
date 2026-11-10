using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Commerce.Application.Identity;
using Commerce.Domain.Identity;
using Commerce.Application.Catalog;
using Commerce.Application.Content;
using Commerce.Infrastructure.Catalog;
using Commerce.Infrastructure.Content;
using Commerce.Infrastructure.Identity;
using Commerce.Infrastructure.Persistence;
using Commerce.Infrastructure.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Commerce.Infrastructure;

public static class DependencyInjection
{
    public const string ReadinessTag = "ready";

    /// <summary>
    /// Registers persistence, object storage and their readiness checks. Configuration is read
    /// lazily and validated when the host starts, so missing settings fail fast with a clear message.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddOptions<DatabaseOptions>()
            .Configure<IConfiguration>((options, configuration) =>
                options.ConnectionString =
                    configuration.GetConnectionString(DatabaseOptions.ConnectionStringName) ?? string.Empty)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddDbContext<CommerceDbContext>((serviceProvider, options) =>
        {
            var database = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            options.UseNpgsql(database.ConnectionString).UseSnakeCaseNamingConvention();
        });

        services.AddOptions<ObjectStorageOptions>()
            .BindConfiguration(ObjectStorageOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IAmazonS3>(serviceProvider =>
            CreateS3Client(serviceProvider.GetRequiredService<IOptions<ObjectStorageOptions>>().Value));

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddSingleton<Commerce.Application.Identity.IPasswordHasher, IdentityPasswordHasher>();
        services.AddScoped<IdentityStore>();
        services.AddScoped<IUserAccounts>(provider => provider.GetRequiredService<IdentityStore>());
        services.AddScoped<IAuthSessions>(provider => provider.GetRequiredService<IdentityStore>());
        services.AddScoped<IOneTimeTokens>(provider => provider.GetRequiredService<IdentityStore>());
        services.AddScoped<ISecurityEvents>(provider => provider.GetRequiredService<IdentityStore>());
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddScoped<IdentitySeeder>();
        services.AddHostedService<IdentityStartupService>();
        services.AddHostedService<SecurityEventRetentionService>();
        services.AddScoped<ICatalogStore, CatalogStore>();
        services.AddScoped<IContentStore, ContentStore>();
        services.AddScoped<ContentService>();
        services.AddScoped<ICatalogReader, CatalogReader>();
        services.AddScoped<IObjectStorage, S3ObjectStorage>();
        services.AddSingleton<IImageDerivatives, SkiaImageDerivatives>();
        services.AddScoped<DerivativePublisher>();
        services.AddScoped<CatalogSeeder>();
        services.AddHostedService<CatalogStartupService>();
        services.AddOptions<CatalogOptions>()
            .BindConfiguration(CatalogOptions.SectionName)
            .ValidateOnStart();

        services.AddOptions<SmtpOptions>().BindConfiguration(SmtpOptions.SectionName);
        services.AddOptions<PublicUrlOptions>()
            .BindConfiguration(PublicUrlOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<AuthLimitOptions>().BindConfiguration(AuthLimitOptions.SectionName);
        services.AddOptions<IdentitySeedOptions>()
            .BindConfiguration(IdentitySeedOptions.SectionName)
            .Validate<IHostEnvironment>(
                (seed, environment) => !seed.BootstrapAdmin
                    || (!environment.IsProduction()
                        && !string.IsNullOrWhiteSpace(seed.AdminEmail)
                        && !string.IsNullOrWhiteSpace(seed.AdminPassword)),
                "Identity:BootstrapAdmin must be disabled in Production and requires Identity:AdminEmail and Identity:AdminPassword.")
            .ValidateOnStart();
        services.AddSingleton(serviceProvider =>
        {
            var auth = serviceProvider.GetRequiredService<IOptions<AuthLimitOptions>>().Value;
            var urls = serviceProvider.GetRequiredService<IOptions<PublicUrlOptions>>().Value;
            return new AuthPolicy(auth.SessionDays, auth.EmailVerificationHours, auth.PasswordResetHours, urls.StorefrontUrl);
        });

        services.AddHealthChecks()
            .AddDbContextCheck<CommerceDbContext>("database", tags: [ReadinessTag])
            .AddCheck<ObjectStorageHealthCheck>(
                "object-storage",
                tags: [ReadinessTag],
                timeout: TimeSpan.FromSeconds(5));

        return services;
    }

    private static AmazonS3Client CreateS3Client(ObjectStorageOptions options)
    {
        var config = new AmazonS3Config
        {
            ForcePathStyle = options.ForcePathStyle,
            // S3-compatible services such as MinIO do not support every default SDK checksum.
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
            Timeout = TimeSpan.FromSeconds(10),
            MaxErrorRetry = 2,
        };

        if (string.IsNullOrWhiteSpace(options.ServiceUrl))
        {
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region);
        }
        else
        {
            config.ServiceURL = options.ServiceUrl;
            config.AuthenticationRegion = options.Region;
        }

        return string.IsNullOrWhiteSpace(options.AccessKey)
            ? new AmazonS3Client(config)
            : new AmazonS3Client(new BasicAWSCredentials(options.AccessKey, options.SecretKey), config);
    }
}
