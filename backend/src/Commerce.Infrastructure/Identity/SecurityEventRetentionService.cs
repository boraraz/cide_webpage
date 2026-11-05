using Commerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Commerce.Infrastructure.Identity;

internal sealed class SecurityEventRetentionService(
    IServiceScopeFactory scopes,
    IOptions<AuthLimitOptions> options,
    TimeProvider time) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var cutoff = time.GetUtcNow().AddDays(-options.Value.SecurityEventRetentionDays);
            await using var scope = scopes.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
            await database.SecurityEvents
                .Where(securityEvent => securityEvent.OccurredAt < cutoff)
                .ExecuteDeleteAsync(stoppingToken);
        }
    }
}
