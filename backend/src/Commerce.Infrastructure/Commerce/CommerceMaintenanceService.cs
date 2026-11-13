using Commerce.Application.Commerce;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Commerce.Infrastructure.Ordering;

public sealed partial class CommerceMaintenanceService(
    IServiceScopeFactory scopes,
    ILogger<CommerceMaintenanceService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunAsync(stoppingToken);
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var maintenance = scope.ServiceProvider.GetRequiredService<ICommerceMaintenance>();
            await maintenance.ExpireDueAsync(cancellationToken);
            await maintenance.DispatchOutboxAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            CommerceMetrics.MaintenanceException();
            LogMaintenanceFailed(logger, exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Commerce maintenance failed.")]
    private static partial void LogMaintenanceFailed(ILogger logger, Exception exception);
}
