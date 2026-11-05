using Commerce.Application.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Commerce.Application;

public static class DependencyInjection
{
    /// <summary>Registers application use cases. Persistence and cryptography are registered by Infrastructure.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IdentityService>();
        return services;
    }
}
