using Commerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Commerce.Infrastructure.Identity;

internal sealed partial class IdentityStartupService(
    IServiceScopeFactory scopes,
    IOptions<IdentitySeedOptions> options,
    ILogger<IdentityStartupService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.SeedRoles && !options.Value.BootstrapAdmin)
        {
            return;
        }

        await using var scope = scopes.CreateAsyncScope();
        if (scope.ServiceProvider.GetService<IHostEnvironment>()?.IsEnvironment("Testing") == true)
        {
            return;
        }
        var database = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var pending = await database.Database.GetPendingMigrationsAsync(cancellationToken);
        if (pending.Any())
        {
            LogPending(logger);
            return;
        }

        await scope.ServiceProvider.GetRequiredService<IdentitySeeder>().SeedAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Warning, Message = "Identity seed skipped because database migrations are pending.")]
    private static partial void LogPending(ILogger logger);
}
