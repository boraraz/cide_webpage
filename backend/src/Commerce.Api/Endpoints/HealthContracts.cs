namespace Commerce.Api.Endpoints;

/// <summary>Liveness status of the API process.</summary>
public sealed record HealthResponse(string Status, string Service, string Version, DateTimeOffset TimestampUtc);

/// <summary>Readiness of the API and its dependencies. Never includes connection details or errors.</summary>
public sealed record ReadinessResponse(string Status, IReadOnlyList<DependencyHealth> Checks);

public sealed record DependencyHealth(string Name, string Status);
