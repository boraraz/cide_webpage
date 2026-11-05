using Microsoft.Extensions.DependencyInjection;

namespace Commerce.Application;

public static class DependencyInjection
{
    /// <summary>Registers application services (use cases). Empty until Phase 2 adds the first module.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services) => services;
}
