namespace Contapop.Ledger.Service.Infrastructure.Outbox;

public sealed class OutboxMessage
{
    public Guid EventId { get; private set; }
    public string EventName { get; private set; } = null!;
    public string AggregateType { get; private set; } = null!;
    public Guid AggregateId { get; private set; }
    public long AggregateVersion { get; private set; }
    public Guid TenantId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public string Payload { get; private set; } = null!;
    public string Status { get; private set; } = null!;
    public int PublishAttempts { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public string? LastError { get; private set; }
    public DateTimeOffset? LockedUntil { get; private set; }

    public static OutboxMessage Create(
        string eventName,
        string aggregateType,
        Guid aggregateId,
        long aggregateVersion,
        Guid tenantId,
        DateTimeOffset occurredAt,
        string payload) => new()
    {
        EventId = Guid.NewGuid(),
        EventName = eventName,
        AggregateType = aggregateType,
        AggregateId = aggregateId,
        AggregateVersion = aggregateVersion,
        TenantId = tenantId,
        OccurredAt = occurredAt,
        Payload = payload,
        Status = "pending",
        PublishAttempts = 0,
    };

    public void MarkDispatched(DateTimeOffset publishedAt)
    {
        Status = "dispatched";
        PublishedAt = publishedAt;
        LockedUntil = null;
        LastError = null;
    }

    public void MarkFailed(string error, DateTimeOffset retryAfter)
    {
        Status = "pending";
        LockedUntil = retryAfter;
        LastError = error.Length > 4_000 ? error[..4_000] : error;
    }
}
