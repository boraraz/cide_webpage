using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Commerce.IntegrationTests;

/// <summary>
/// Hosts the API in-memory with self-contained settings. Dependencies point at a closed port so
/// tests never depend on local infrastructure or developer secrets.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string AllowedOrigin = "http://localhost:5173";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Database",
            "Host=127.0.0.1;Port=1;Database=unreachable;Username=test;Password=test;Timeout=2");
        builder.UseSetting("ObjectStorage:ServiceUrl", "http://127.0.0.1:1");
        builder.UseSetting("ObjectStorage:BucketName", "test-bucket");
        builder.UseSetting("ObjectStorage:AccessKey", "test");
        builder.UseSetting("ObjectStorage:SecretKey", "test-secret");
        builder.UseSetting("ObjectStorage:ForcePathStyle", "true");
        builder.UseSetting("Cors:AllowedOrigins:0", AllowedOrigin);
    }
}

/// <summary>Uses the developer's real Development configuration and user secrets.</summary>
public sealed class LocalInfrastructureApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Development");
}
