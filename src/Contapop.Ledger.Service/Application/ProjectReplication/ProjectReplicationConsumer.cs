using Contapop.Ledger.Service.Infrastructure.Messaging;
using Contapop.Ledger.Service.Infrastructure.Persistence;
using Contapop.Ledger.Service.Infrastructure.Replication;
using Microsoft.EntityFrameworkCore;

namespace Contapop.Ledger.Service.Application.ProjectReplication;

public sealed class ProjectReplicationConsumer(LedgerDbContext database)
{
    public const string ConsumerName = "ledger.project-replica";

    public async Task ConsumeAsync(IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
    {
        if (!IsProjectLifecycleEvent(envelope.EventName))
        {
            return;
        }

        await using var databaseTransaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var alreadyProcessed = await database.InboxMessages.AnyAsync(
            message => message.ConsumerName == ConsumerName && message.EventId == envelope.EventId,
            cancellationToken);
        if (alreadyProcessed)
        {
            await databaseTransaction.RollbackAsync(cancellationToken);
            return;
        }

        var projectId = GetGuid(envelope.Payload, "project_id", envelope.AggregateId);
        var tenantId = GetGuid(envelope.Payload, "tenant_id", envelope.TenantId);
        var replica = await database.ProjectReplicas.SingleOrDefaultAsync(
            project => project.ProjectId == projectId,
            cancellationToken);

        if (replica is null && envelope.EventName == "identity.project-created.v1")
        {
            database.ProjectReplicas.Add(ProjectReplica.Create(
                projectId,
                tenantId,
                GetRequiredString(envelope.Payload, "name"),
                GetOptionalString(envelope.Payload, "status") ?? "active",
                envelope.AggregateVersion,
                GetTimestamp(envelope.Payload, "created_at", envelope.OccurredAt)));
        }
        else if (replica is not null)
        {
            var (name, status, timestamp) = envelope.EventName switch
            {
                "identity.project-renamed.v1" => (GetRequiredString(envelope.Payload, "name"), replica.Status, GetTimestamp(envelope.Payload, "renamed_at", envelope.OccurredAt)),
                "identity.project-archived.v1" => ((string?)null, "archived", GetTimestamp(envelope.Payload, "archived_at", envelope.OccurredAt)),
                "identity.project-reactivated.v1" => ((string?)null, "active", GetTimestamp(envelope.Payload, "reactivated_at", envelope.OccurredAt)),
                _ => ((string?)null, replica.Status, envelope.OccurredAt),
            };
            replica.Apply(name, status, envelope.AggregateVersion, timestamp);
        }

        database.InboxMessages.Add(InboxMessage.Processed(envelope.EventId, ConsumerName, DateTimeOffset.UtcNow));
        await database.SaveChangesAsync(cancellationToken);
        await databaseTransaction.CommitAsync(cancellationToken);
    }

    private static bool IsProjectLifecycleEvent(string eventName) => eventName is
        "identity.project-created.v1" or
        "identity.project-renamed.v1" or
        "identity.project-archived.v1" or
        "identity.project-reactivated.v1";

    private static Guid GetGuid(System.Text.Json.JsonElement payload, string propertyName, Guid fallback) =>
        payload.TryGetProperty(propertyName, out var property) && property.TryGetGuid(out var value) ? value : fallback;

    private static string GetRequiredString(System.Text.Json.JsonElement payload, string propertyName) =>
        GetOptionalString(payload, propertyName) ?? throw new InvalidOperationException($"Integration event payload is missing '{propertyName}'.");

    private static string? GetOptionalString(System.Text.Json.JsonElement payload, string propertyName) =>
        payload.TryGetProperty(propertyName, out var property) ? property.GetString() : null;

    private static DateTimeOffset GetTimestamp(System.Text.Json.JsonElement payload, string propertyName, DateTimeOffset fallback) =>
        payload.TryGetProperty(propertyName, out var property) && property.TryGetDateTimeOffset(out var value) ? value : fallback;
}
