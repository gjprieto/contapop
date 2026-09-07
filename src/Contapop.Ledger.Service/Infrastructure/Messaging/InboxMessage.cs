namespace Contapop.Ledger.Service.Infrastructure.Messaging;

public sealed class InboxMessage
{
    public Guid EventId { get; private set; }
    public string ConsumerName { get; private set; } = null!;
    public DateTimeOffset ProcessedAt { get; private set; }

    public static InboxMessage Processed(Guid eventId, string consumerName, DateTimeOffset processedAt) => new()
    {
        EventId = eventId,
        ConsumerName = consumerName,
        ProcessedAt = processedAt,
    };
}
