using System.Net;
using System.Net.Http.Json;
using Commerce.Api.Endpoints;
using Commerce.Application.Catalog;
using Microsoft.Extensions.DependencyInjection;

namespace Commerce.IntegrationTests;

/// <summary>
/// Runs against the Docker Compose PostgreSQL and MinIO services using the developer's user
/// secrets. Opt in with COMMERCE_INFRA_TESTS=1 after `npm run infra:up` and `npm run api:secrets`.
/// </summary>
public sealed class LocalInfrastructureTests
{
    private static readonly bool Enabled = Environment.GetEnvironmentVariable("COMMERCE_INFRA_TESTS") == "1";

    [Fact]
    public async Task Readiness_is_healthy_when_local_infrastructure_is_running()
    {
        Assert.SkipUnless(Enabled, "Set COMMERCE_INFRA_TESTS=1 to run tests against local Docker infrastructure.");

        await using var factory = new LocalInfrastructureApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/health/ready", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<ReadinessResponse>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.All(body.Checks, check => Assert.Equal("Healthy", check.Status));
    }

    [Fact]
    public async Task Object_storage_puts_and_deletes_one_catalog_test_object()
    {
        Assert.SkipUnless(Enabled, "Set COMMERCE_INFRA_TESTS=1 to run tests against local Docker infrastructure.");

        await using var factory = new LocalInfrastructureApiFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var storage = scope.ServiceProvider.GetRequiredService<IObjectStorage>();
        var key = $"catalog-tests/{Guid.CreateVersion7():N}.png";
        ReadOnlyMemory<byte> png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        await storage.PutAsync(key, png, "image/png", TestContext.Current.CancellationToken);
        var stored = await storage.OpenAsync(key, TestContext.Current.CancellationToken);
        Assert.NotNull(stored);
        Assert.Equal(png.ToArray(), stored.Content);

        await storage.DeleteAsync(key, TestContext.Current.CancellationToken);
        Assert.Null(await storage.OpenAsync(key, TestContext.Current.CancellationToken));
    }
}
