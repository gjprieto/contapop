using Contapop.Bookkeeping.Service.Infrastructure.Persistence;
using Contapop.Bookkeeping.Service.Infrastructure.Replication;
using Microsoft.EntityFrameworkCore;

namespace Contapop.Bookkeeping.Service.Application.Replication;

public sealed class TransactionReplicationConsumer(BookkeepingDbContext database)
{
    public const string ConsumerName = "bookkeeping.transaction-replica";

    public async Task ConsumeAsync(IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
    {
        if (envelope.EventName is not ("ledger.transaction-recorded.v1" or "ledger.transaction-updated.v1" or "ledger.transaction-archived.v1")) return;

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (await database.InboxMessages.AnyAsync(message => message.ConsumerName == ConsumerName && message.EventId == envelope.EventId, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return;
        }

        var transactionId = EventPayload.GetGuid(envelope.Payload, "transaction_id", envelope.AggregateId);
        var replica = await database.TransactionReplicas.SingleOrDefaultAsync(item => item.TransactionId == transactionId, cancellationToken);
        if (replica is null && envelope.EventName == "ledger.transaction-recorded.v1")
        {
            database.TransactionReplicas.Add(TransactionReplica.Create(
                transactionId,
                EventPayload.GetGuid(envelope.Payload, "tenant_id", envelope.TenantId),
                EventPayload.GetRequiredGuid(envelope.Payload, "bank_account_id"),
                EventPayload.GetRequiredInt64(envelope.Payload, "amount"),
                EventPayload.GetRequiredDate(envelope.Payload, "date"),
                EventPayload.GetRequiredString(envelope.Payload, "type"),
                EventPayload.GetOptionalString(envelope.Payload, "description"),
                EventPayload.GetOptionalString(envelope.Payload, "status") ?? "active",
                envelope.AggregateVersion,
                EventPayload.GetTimestamp(envelope.Payload, "created_at", envelope.OccurredAt)));
        }
        else if (replica is not null)
        {
            if (envelope.EventName == "ledger.transaction-updated.v1")
            {
                replica.Apply(
                    EventPayload.GetOptionalGuid(envelope.Payload, "bank_account_id"),
                    EventPayload.GetOptionalInt64(envelope.Payload, "amount"),
                    EventPayload.GetOptionalDate(envelope.Payload, "date"),
                    EventPayload.GetOptionalString(envelope.Payload, "type"),
                    EventPayload.GetOptionalString(envelope.Payload, "description"),
                    true,
                    EventPayload.GetOptionalString(envelope.Payload, "status") ?? "active",
                    envelope.AggregateVersion,
                    EventPayload.GetTimestamp(envelope.Payload, "updated_at", envelope.OccurredAt));
            }
            else if (envelope.EventName == "ledger.transaction-archived.v1")
            {
                replica.Apply(null, null, null, null, null, false, "archived", envelope.AggregateVersion,
                    EventPayload.GetTimestamp(envelope.Payload, "archived_at", envelope.OccurredAt));
            }
        }

        database.InboxMessages.Add(InboxMessage.Processed(envelope.EventId, ConsumerName, DateTimeOffset.UtcNow));
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
