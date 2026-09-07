namespace Contapop.Ledger.Service.Domain.Transactions;

public sealed class Transaction
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid BankAccountId { get; private set; }
    public long AmountMinor { get; private set; }
    public DateOnly Date { get; private set; }
    public string Type { get; private set; } = null!;
    public string Status { get; private set; } = null!;
    public long Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
}
