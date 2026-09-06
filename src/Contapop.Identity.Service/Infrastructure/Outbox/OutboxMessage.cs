namespace Contapop.Identity.Service.Infrastructure.Outbox;

public sealed class OutboxMessage
{
    public Guid EventId { get; private set; }
    public string EventName { get; private set; } = null!;
    public string AggregateType { get; private set; } = null!;
    public Guid AggregateId { get; private set; }
    public long AggregateVersion { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid? ActorId { get; private set; }
    public Guid? CorrelationId { get; private set; }
    public Guid? CausationId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public string Payload { get; private set; } = null!;
    public string Status { get; private set; } = null!;
    public int PublishAttempts { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public string? LastError { get; private set; }
}
