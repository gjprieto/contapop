using Contapop.Billing.Service.Infrastructure.Persistence;
using Contapop.Billing.Service.Infrastructure.Replication;
using Microsoft.EntityFrameworkCore;

namespace Contapop.Billing.Service.Application.Replication;

public sealed class ProjectReplicationConsumer(BillingDbContext database)
{
    public const string ConsumerName = "billing.project-replica";

    public async Task ConsumeAsync(IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
    {
        if (envelope.EventName is not ("identity.project-created.v1" or "identity.project-renamed.v1" or "identity.project-archived.v1" or "identity.project-reactivated.v1")) return;

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (await database.InboxMessages.AnyAsync(message => message.ConsumerName == ConsumerName && message.EventId == envelope.EventId, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return;
        }

        var projectId = EventPayload.GetGuid(envelope.Payload, "project_id", envelope.AggregateId);
        var replica = await database.ProjectReplicas.SingleOrDefaultAsync(project => project.ProjectId == projectId, cancellationToken);
        if (replica is null && envelope.EventName == "identity.project-created.v1")
        {
            database.ProjectReplicas.Add(ProjectReplica.Create(
                projectId,
                EventPayload.GetGuid(envelope.Payload, "tenant_id", envelope.TenantId),
                EventPayload.GetRequiredString(envelope.Payload, "name"),
                EventPayload.GetOptionalString(envelope.Payload, "status") ?? "active",
                envelope.AggregateVersion,
                EventPayload.GetTimestamp(envelope.Payload, "created_at", envelope.OccurredAt)));
        }
        else if (replica is not null)
        {
            var (name, status, changedAt) = envelope.EventName switch
            {
                "identity.project-renamed.v1" => (EventPayload.GetRequiredString(envelope.Payload, "name"), replica.Status, EventPayload.GetTimestamp(envelope.Payload, "renamed_at", envelope.OccurredAt)),
                "identity.project-archived.v1" => ((string?)null, "archived", EventPayload.GetTimestamp(envelope.Payload, "archived_at", envelope.OccurredAt)),
                "identity.project-reactivated.v1" => ((string?)null, "active", EventPayload.GetTimestamp(envelope.Payload, "reactivated_at", envelope.OccurredAt)),
                _ => ((string?)null, replica.Status, envelope.OccurredAt),
            };
            replica.Apply(name, status, envelope.AggregateVersion, changedAt);
        }

        database.InboxMessages.Add(InboxMessage.Processed(envelope.EventId, ConsumerName, DateTimeOffset.UtcNow));
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
