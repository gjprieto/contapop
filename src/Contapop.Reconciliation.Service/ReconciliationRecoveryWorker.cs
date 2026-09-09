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
            var operationIds = await database.Operations.AsNoTracking()
                .Where(operation => operation.Status != "completed" && operation.NextAttemptAt <= DateTimeOffset.UtcNow)
                .OrderBy(operation => operation.NextAttemptAt).Select(operation => operation.Id).Take(20).ToListAsync(stoppingToken);
            if (operationIds.Count > 0) logger.LogWarning("Recovering {Count} reconciliation operations.", operationIds.Count);
            var coordinator = scope.ServiceProvider.GetRequiredService<Reconciliation.PaymentReconciliationCoordinator>();
            foreach (var operationId in operationIds) await coordinator.ExecuteAsync(operationId, stoppingToken);
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
