namespace Contapop.Billing.Service.Infrastructure.Persistence;

public sealed class Invoice
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid CounterpartyId { get; private set; }
    public string Direction { get; private set; } = null!;
    public string Status { get; private set; } = null!;
    public long NetAmountMinor { get; private set; }
    public decimal TaxRate { get; private set; }
    public long TaxAmountMinor { get; private set; }
    public long TotalAmountMinor { get; private set; }
    public DateOnly Date { get; private set; }
    public DateOnly DueDate { get; private set; }
    public long Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
}

public sealed class Payment
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid InvoiceId { get; private set; }
    public Guid? ReconciledTransactionId { get; private set; }
    public long AmountMinor { get; private set; }
    public DateOnly Date { get; private set; }
    public string PaymentMethod { get; private set; } = null!;
    public long Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
}

public sealed class Counterparty
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Type { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? TaxId { get; private set; }
    public string? Email { get; private set; }
    public string? Address { get; private set; }
    public string Status { get; private set; } = null!;
    public long Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
}

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
