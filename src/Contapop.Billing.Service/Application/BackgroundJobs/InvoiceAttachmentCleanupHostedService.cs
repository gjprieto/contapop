using Contapop.Billing.Service.Infrastructure.Attachments;
using Contapop.Billing.Service.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Contapop.Billing.Service.Application.BackgroundJobs;

public sealed class InvoiceAttachmentCleanupHostedService(IServiceScopeFactory scopes, ILogger<InvoiceAttachmentCleanupHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { await ProcessAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception) { logger.LogError(exception, "Invoice attachment cleanup failed."); }
        }
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<InvoiceAttachmentStorage>();
        var entries = await database.AttachmentCleanups.Where(item => item.NextAttemptAt <= DateTimeOffset.UtcNow).OrderBy(item => item.CreatedAt).Take(20).ToListAsync(cancellationToken);
        foreach (var entry in entries)
        {
            try { await storage.DeleteAsync(entry.BlobName, cancellationToken); database.AttachmentCleanups.Remove(entry); }
            catch (Exception) { entry.Retry(DateTimeOffset.UtcNow); }
        }
        await database.SaveChangesAsync(cancellationToken);
    }
}
