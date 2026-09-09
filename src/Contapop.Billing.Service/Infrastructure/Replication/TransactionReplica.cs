namespace Contapop.Billing.Service.Infrastructure.Replication;

public sealed class TransactionReplica
{
    public Guid TransactionId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid BankAccountId { get; private set; }
    public long AmountMinor { get; private set; }
    public DateOnly Date { get; private set; }
    public string Type { get; private set; } = null!;
    public string? Description { get; private set; }
    public string Status { get; private set; } = null!;
    public long AggregateVersion { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static TransactionReplica Create(Guid transactionId, Guid tenantId, Guid bankAccountId, long amountMinor, DateOnly date, string type, string? description, string status, long aggregateVersion, DateTimeOffset createdAt) => new()
    {
        TransactionId = transactionId, TenantId = tenantId, BankAccountId = bankAccountId,
        AmountMinor = amountMinor, Date = date, Type = type, Description = description, Status = status,
        AggregateVersion = aggregateVersion, CreatedAt = createdAt, UpdatedAt = createdAt,
    };

    public void Apply(Guid? bankAccountId, long? amountMinor, DateOnly? date, string? type, string? description, bool updateDescription, string status, long aggregateVersion, DateTimeOffset updatedAt)
    {
        if (aggregateVersion <= AggregateVersion) return;
        if (bankAccountId.HasValue) BankAccountId = bankAccountId.Value;
        if (amountMinor.HasValue) AmountMinor = amountMinor.Value;
        if (date.HasValue) Date = date.Value;
        if (type is not null) Type = type;
        if (updateDescription) Description = description;
        Status = status;
        AggregateVersion = aggregateVersion;
        UpdatedAt = updatedAt;
    }
}
