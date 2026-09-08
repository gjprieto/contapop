using Contapop.Reconciliation.Service.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Contapop.Reconciliation.Service;
public sealed class ReconciliationRecoveryWorker(IServiceScopeFactory scopes, ILogger<ReconciliationRecoveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await using var scope = scopes.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<ReconciliationDbContext>();
            var blocked = await database.Operations.CountAsync(operation => operation.Status == "reserved" || operation.Status == "dependent-write-succeeded" || operation.Status == "dependent-write-failed", stoppingToken);
            if (blocked > 0) logger.LogWarning("{Count} reconciliation operations require recovery.", blocked);
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
