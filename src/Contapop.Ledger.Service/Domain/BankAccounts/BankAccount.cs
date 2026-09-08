using Contapop.Ledger.Service.Domain.Events;

namespace Contapop.Ledger.Service.Domain.BankAccounts;

public sealed class BankAccount
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string AccountNumber { get; private set; } = null!;
    public string BankName { get; private set; } = null!;
    public string Status { get; private set; } = null!;
    public int Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    private List<object> DomainEvents { get; } = [];

    public static BankAccount Create(Guid tenantId, Guid projectId, string accountNumber, string bankName, DateTimeOffset now)
    {
        var account = new BankAccount
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = projectId,
            AccountNumber = accountNumber,
            BankName = bankName,
            Status = "active",
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now,
        };
        account.DomainEvents.Add(new BankAccountLinked(account.Id, account.TenantId, account.ProjectId, account.BankName, account.Status, now));
        return account;
    }

    public IReadOnlyCollection<object> GetDomainEvents() => DomainEvents;
    public void ClearDomainEvents() => DomainEvents.Clear();

    public bool TryArchive(int expectedVersion, DateTimeOffset now)
    {
        if (Status != "active" || Version != expectedVersion)
        {
            return false;
        }

        Status = "archived";
        Version++;
        UpdatedAt = now;
        return true;
    }
}
