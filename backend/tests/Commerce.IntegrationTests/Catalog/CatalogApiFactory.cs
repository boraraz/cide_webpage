using Commerce.Application.Catalog;
using Commerce.Infrastructure.Identity;
using Commerce.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace Commerce.IntegrationTests.Catalog;

public sealed class CatalogApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine").Build();

    public MemoryObjectStorage Storage { get; } = new();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();
        using var scope = Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        await database.Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<IdentitySeeder>().SeedAsync(CancellationToken.None);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Database", _postgres.GetConnectionString());
        builder.UseSetting("ObjectStorage:ServiceUrl", "http://127.0.0.1:1");
        builder.UseSetting("ObjectStorage:BucketName", "test-bucket");
        builder.UseSetting("ObjectStorage:AccessKey", "test");
        builder.UseSetting("ObjectStorage:SecretKey", "test-secret");
        builder.UseSetting("ObjectStorage:ForcePathStyle", "true");
        builder.UseSetting("Cors:AllowedOrigins:0", "http://localhost:5173");
        builder.UseSetting("Identity:SeedRoles", "true");
        builder.UseSetting("Identity:BootstrapAdmin", "true");
        builder.UseSetting("Identity:AdminEmail", CatalogHttp.AdminEmail);
        builder.UseSetting("Identity:AdminPassword", CatalogHttp.Password);
        builder.UseSetting("Catalog:SeedSampleData", "false");
        builder.UseSetting("Catalog:MediaUploadPermitLimit", "1000");
        builder.UseSetting("PublicUrls:StorefrontUrl", "http://localhost:5173");
        builder.UseSetting("Auth:RegisterPermitLimit", "1000");
        builder.UseSetting("Auth:LoginPermitLimit", "1000");
        builder.UseSetting("Auth:VerificationPermitLimit", "1000");
        builder.UseSetting("Auth:PasswordResetPermitLimit", "1000");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IObjectStorage>();
            services.AddSingleton<IObjectStorage>(Storage);
        });
    }
}

[CollectionDefinition(CatalogCollection.Name, DisableParallelization = true)]
public sealed class CatalogCollection : ICollectionFixture<CatalogApiFactory>
{
    public const string Name = "catalog";
}
