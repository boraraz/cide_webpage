using Commerce.Application.Identity;
using Commerce.Infrastructure.Identity;
using Commerce.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Commerce.IntegrationTests.Identity;

public class IdentityApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine").Build();

    public CapturingEmailSender Email { get; } = new();

    protected virtual bool TightLimits => false;

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
        builder.UseSetting("Identity:BootstrapAdmin", "false");
        builder.UseSetting("PublicUrls:StorefrontUrl", "http://localhost:5173");
        if (TightLimits)
        {
            builder.UseSetting("Auth:RegisterPermitLimit", "2");
            builder.UseSetting("Auth:RegisterWindowMinutes", "60");
        }
        else
        {
            builder.UseSetting("Auth:RegisterPermitLimit", "1000");
            builder.UseSetting("Auth:LoginPermitLimit", "1000");
            builder.UseSetting("Auth:VerificationPermitLimit", "1000");
            builder.UseSetting("Auth:PasswordResetPermitLimit", "1000");
        }

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(Email);
            services.AddSingleton<IEmailSender>(Email);
        });
    }
}

public sealed class TightLimitApiFactory : IdentityApiFactory
{
    protected override bool TightLimits => true;
}

[CollectionDefinition(IdentityCollection.Name, DisableParallelization = true)]
public sealed class IdentityCollection : ICollectionFixture<IdentityApiFactory>
{
    public const string Name = "identity";
}
