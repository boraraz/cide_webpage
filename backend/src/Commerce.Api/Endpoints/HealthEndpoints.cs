using System.Reflection;
using Commerce.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Commerce.Api.Endpoints;

public static class HealthEndpoints
{
    private const string ServiceName = "Commerce.Api";

    private static readonly string Version =
        typeof(HealthEndpoints).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? "unknown";

    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/health").WithTags("Health");

        group.MapGet("", GetLiveness)
            .WithName("GetHealth")
            .WithSummary("Liveness: the API process is running. Does not check dependencies.");

        group.MapGet("/ready", GetReadinessAsync)
            .WithName("GetReadiness")
            .WithSummary("Readiness: the API and its database and object storage dependencies are available.")
            .Produces<ReadinessResponse>(StatusCodes.Status503ServiceUnavailable);

        return app;
    }

    private static Ok<HealthResponse> GetLiveness(TimeProvider timeProvider) =>
        TypedResults.Ok(new HealthResponse("Healthy", ServiceName, Version, timeProvider.GetUtcNow()));

    private static async Task<Results<Ok<ReadinessResponse>, JsonHttpResult<ReadinessResponse>>> GetReadinessAsync(
        HealthCheckService healthCheckService,
        CancellationToken cancellationToken)
    {
        var report = await healthCheckService.CheckHealthAsync(
            registration => registration.Tags.Contains(DependencyInjection.ReadinessTag),
            cancellationToken);

        var response = new ReadinessResponse(
            report.Status.ToString(),
            [.. report.Entries.Select(entry => new DependencyHealth(entry.Key, entry.Value.Status.ToString()))]);

        return report.Status == HealthStatus.Unhealthy
            ? TypedResults.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable)
            : TypedResults.Ok(response);
    }
}
