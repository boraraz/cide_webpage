using Commerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Commerce.Infrastructure.Catalog;

internal sealed partial class CatalogStartupService(
    IServiceScopeFactory scopes,
    IOptions<CatalogOptions> options,
    ILogger<CatalogStartupService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.SeedSampleData)
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

        await scope.ServiceProvider.GetRequiredService<CatalogSeeder>().SeedAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Warning, Message = "Catalog seed skipped because database migrations are pending.")]
    private static partial void LogPending(ILogger logger);
}
