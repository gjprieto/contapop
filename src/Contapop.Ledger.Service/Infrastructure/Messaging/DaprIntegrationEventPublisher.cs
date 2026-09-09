using System.Text.Json;
using Contapop.Ledger.Service.Application.Abstractions;
using Contapop.Ledger.Service.Infrastructure.Outbox;
using Dapr.Client;

namespace Contapop.Ledger.Service.Infrastructure.Messaging;

public sealed class DaprIntegrationEventPublisher(DaprClient daprClient) : IIntegrationEventPublisher
{
    public Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken) =>
        daprClient.PublishEventAsync("pubsub", "ledger.events", new
        {
            event_id = message.EventId,
            event_name = message.EventName,
            aggregate_type = message.AggregateType,
            aggregate_id = message.AggregateId,
            aggregate_version = message.AggregateVersion,
            tenant_id = message.TenantId,
            occurred_at = message.OccurredAt,
            correlation_id = (Guid?)null,
            causation_id = (Guid?)null,
            payload = JsonSerializer.Deserialize<JsonElement>(message.Payload),
        }, cancellationToken);
}
