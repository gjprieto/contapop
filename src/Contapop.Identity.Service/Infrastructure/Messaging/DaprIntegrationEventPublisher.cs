using System.Text.Json;
using Contapop.Identity.Service.Application.Abstractions;
using Contapop.Identity.Service.Infrastructure.Outbox;
using Dapr.Client;

namespace Contapop.Identity.Service.Infrastructure.Messaging;

public sealed class DaprIntegrationEventPublisher(DaprClient daprClient) : IIntegrationEventPublisher
{
    public Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken) =>
        daprClient.PublishEventAsync("pubsub", "identity.events", new
        {
            event_id = message.EventId,
            event_name = message.EventName,
            aggregate_type = message.AggregateType,
            aggregate_id = message.AggregateId,
            aggregate_version = message.AggregateVersion,
            tenant_id = message.TenantId,
            occurred_at = message.OccurredAt,
            correlation_id = message.CorrelationId,
            causation_id = message.CausationId,
            payload = JsonSerializer.Deserialize<JsonElement>(message.Payload),
        }, cancellationToken);
}
