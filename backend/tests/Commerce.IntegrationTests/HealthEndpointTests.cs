using System.Net;
using System.Net.Http.Json;
using Commerce.Api.Endpoints;

namespace Commerce.IntegrationTests;

public sealed class HealthEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Liveness_returns_healthy_status()
    {
        var response = await _client.GetAsync("/api/v1/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Equal("Healthy", body.Status);
        Assert.Equal("Commerce.Api", body.Service);
        Assert.False(string.IsNullOrWhiteSpace(body.Version));
    }

    [Fact]
    public async Task Readiness_returns_503_without_leaking_details_when_dependencies_are_down()
    {
        var response = await _client.GetAsync("/api/v1/health/ready", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("Password", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("127.0.0.1", raw, StringComparison.Ordinal);

        var body = await response.Content.ReadFromJsonAsync<ReadinessResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Equal("Unhealthy", body.Status);
        Assert.Equal(["database", "object-storage"], body.Checks.Select(check => check.Name).Order());
        Assert.All(body.Checks, check => Assert.Equal("Unhealthy", check.Status));
    }

    [Fact]
    public async Task Unknown_routes_return_problem_details()
    {
        var response = await _client.GetAsync("/api/v1/does-not-exist", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task OpenApi_document_is_not_exposed_outside_development()
    {
        var response = await _client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
