using System.Text.Json;
using System.Text.Json.Serialization;

namespace Contapop.Bookkeeping.Service.Application.Replication;

public sealed record IntegrationEventEnvelope(
    [property: JsonPropertyName("event_id")] Guid EventId,
    [property: JsonPropertyName("event_name")] string EventName,
    [property: JsonPropertyName("aggregate_type")] string AggregateType,
    [property: JsonPropertyName("aggregate_id")] Guid AggregateId,
    [property: JsonPropertyName("aggregate_version")] long AggregateVersion,
    [property: JsonPropertyName("tenant_id")] Guid TenantId,
    [property: JsonPropertyName("occurred_at")] DateTimeOffset OccurredAt,
    [property: JsonPropertyName("payload")] JsonElement Payload);
