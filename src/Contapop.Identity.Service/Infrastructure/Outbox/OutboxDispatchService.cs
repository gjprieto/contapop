namespace Contapop.Identity.Service.Infrastructure.Outbox;

public sealed class OutboxDispatchService(IServiceScopeFactory scopeFactory, ILogger<OutboxDispatchService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<OutboxDispatcher>();
                await dispatcher.DispatchPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Outbox dispatch cycle failed.");
            }
        }
    }
}
