using Amazon.S3;
using Commerce.Infrastructure;
using Commerce.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Commerce.UnitTests.Infrastructure;

public sealed class InfrastructureRegistrationTests
{
    private static readonly Dictionary<string, string?> ValidSettings = new()
    {
        ["ConnectionStrings:Database"] = "Host=localhost;Database=test;Username=test;Password=test",
        ["ObjectStorage:ServiceUrl"] = "http://localhost:9000",
        ["ObjectStorage:BucketName"] = "test-bucket",
        ["ObjectStorage:AccessKey"] = "test",
        ["ObjectStorage:SecretKey"] = "test-secret",
        ["ObjectStorage:ForcePathStyle"] = "true",
    };

    [Fact]
    public void Registers_database_storage_and_readiness_checks()
    {
        using var provider = BuildProvider(ValidSettings);
        using var scope = provider.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", dbContext.Database.ProviderName);

        var s3 = provider.GetRequiredService<IAmazonS3>();
        Assert.True(((AmazonS3Config)s3.Config).ForcePathStyle);

        var healthChecks = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;
        Assert.Equal(["database", "object-storage"], healthChecks.Select(check => check.Name).Order());
        Assert.All(healthChecks, check => Assert.Contains(DependencyInjection.ReadinessTag, check.Tags));
    }

    [Theory]
    [InlineData("ConnectionStrings:Database", "ConnectionStrings:Database is not configured")]
    [InlineData("ObjectStorage:BucketName", "ObjectStorage:BucketName is not configured")]
    public void Missing_required_settings_fail_validation_with_a_clear_message(string missingKey, string expectedMessage)
    {
        var settings = new Dictionary<string, string?>(ValidSettings) { [missingKey] = null };
        using var provider = BuildProvider(settings);

        var exception = Assert.ThrowsAny<OptionsValidationException>(() =>
        {
            _ = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            _ = provider.GetRequiredService<IOptions<Commerce.Infrastructure.Storage.ObjectStorageOptions>>().Value;
        });
        Assert.Contains(expectedMessage, exception.Message, StringComparison.Ordinal);
    }

    private static ServiceProvider BuildProvider(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        services.AddLogging();
        services.AddInfrastructure();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "Commerce.Api";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
