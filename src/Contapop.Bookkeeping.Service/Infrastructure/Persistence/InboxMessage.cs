namespace Contapop.Bookkeeping.Service.Infrastructure.Persistence;

public sealed class InboxMessage
{
    public string ConsumerName { get; private set; } = null!;
    public Guid EventId { get; private set; }
    public DateTimeOffset ProcessedAt { get; private set; }

    public static InboxMessage Processed(Guid eventId, string consumerName, DateTimeOffset processedAt) => new()
    {
        EventId = eventId,
        ConsumerName = consumerName,
        ProcessedAt = processedAt,
    };
}
