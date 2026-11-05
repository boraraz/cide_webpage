using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Commerce.Infrastructure.Persistence;
using Commerce.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
            options.UseNpgsql(database.ConnectionString);
        });

        services.AddOptions<ObjectStorageOptions>()
            .BindConfiguration(ObjectStorageOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IAmazonS3>(serviceProvider =>
            CreateS3Client(serviceProvider.GetRequiredService<IOptions<ObjectStorageOptions>>().Value));

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
