using System.Text.Json;
using Contapop.Billing.Service.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Contapop.Billing.Service.Application.BackgroundJobs;

public sealed class MarkInvoicesOverdueJob(IServiceScopeFactory scopeFactory, TimeProvider timeProvider, ILogger<MarkInvoicesOverdueJob> logger)
{
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var currentDate = DateOnly.FromDateTime(now.UtcDateTime);

        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var invoices = await database.Invoices
            .Where(invoice => invoice.Status == "issued" && invoice.DueDate < currentDate)
            .ToListAsync(cancellationToken);

        foreach (var invoice in invoices)
        {
            if (!invoice.TryMarkOverdue(currentDate, now)) continue;

            database.OutboxMessages.Add(OutboxMessage.Create(
                "billing.invoice-overdue.v1",
                "Invoice",
                invoice.Id,
                invoice.Version,
                invoice.TenantId,
                now,
                JsonSerializer.Serialize(new
                {
                    invoice_id = invoice.Id,
                    project_id = invoice.ProjectId,
                    direction = invoice.Direction,
                    total_amount_minor = invoice.TotalAmountMinor,
                    due_date = invoice.DueDate,
                    overdue_at = now,
                })));
        }

        if (invoices.Count > 0)
        {
            await database.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Marked {InvoiceCount} invoices as overdue.", invoices.Count);
        }

        return invoices.Count;
    }
}

public sealed class MarkInvoicesOverdueHostedService(MarkInvoicesOverdueJob job, ILogger<MarkInvoicesOverdueHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));

        do
        {
            try
            {
                await job.RunAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to mark overdue invoices.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
