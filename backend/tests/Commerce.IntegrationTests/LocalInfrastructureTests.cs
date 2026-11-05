using System.Net;
using System.Net.Http.Json;
using Commerce.Api.Endpoints;

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
}
