using Contapop.Identity.Service.Application.Abstractions;
using Contapop.Identity.Service.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Contapop.Identity.Service.Infrastructure.Outbox;

public sealed class OutboxDispatcher(
    IDbContextFactory<IdentityDbContext> databaseFactory,
    IIntegrationEventPublisher publisher,
    ILogger<OutboxDispatcher> logger)
{
    public async Task DispatchPendingAsync(CancellationToken cancellationToken)
    {
        for (var index = 0; index < 20; index++)
        {
            await using var database = await databaseFactory.CreateDbContextAsync(cancellationToken);
            var message = await ClaimNextAsync(database, cancellationToken);
            if (message is null)
            {
                return;
            }

            try
            {
                await publisher.PublishAsync(message, cancellationToken);
                message.MarkDispatched(DateTimeOffset.UtcNow);
                await database.SaveChangesAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Unable to publish outbox message {EventId}", message.EventId);
                message.MarkFailed(exception.Message, DateTimeOffset.UtcNow.AddMinutes(1));
                await database.SaveChangesAsync(CancellationToken.None);
            }
        }
    }

    private static async Task<OutboxMessage?> ClaimNextAsync(IdentityDbContext database, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var candidate = await database.OutboxMessages
            .AsNoTracking()
            .Where(message => (message.Status == "pending" && (message.LockedUntil == null || message.LockedUntil < now)) || (message.Status == "processing" && message.LockedUntil < now))
            .OrderBy(message => message.OccurredAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (candidate is null)
        {
            return null;
        }

        var claimed = await database.OutboxMessages
            .Where(message => message.EventId == candidate.EventId)
            .Where(message => (message.Status == "pending" && (message.LockedUntil == null || message.LockedUntil < now)) || (message.Status == "processing" && message.LockedUntil < now))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(message => message.Status, "processing")
                .SetProperty(message => message.LockedUntil, now.AddMinutes(1))
                .SetProperty(message => message.PublishAttempts, message => message.PublishAttempts + 1), cancellationToken);
        if (claimed == 0)
        {
            return null;
        }

        database.ChangeTracker.Clear();
        return await database.OutboxMessages.SingleAsync(message => message.EventId == candidate.EventId, cancellationToken);
    }
}
