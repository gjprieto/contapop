namespace Contapop.Ledger.Service.Domain.BankAccounts;

public sealed class BankAccount
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string AccountNumber { get; private set; } = null!;
    public string BankName { get; private set; } = null!;
    public string Status { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
}
