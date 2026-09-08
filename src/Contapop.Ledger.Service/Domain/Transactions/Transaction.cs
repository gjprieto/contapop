using Contapop.Ledger.Service.Domain.Events;

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
    private List<object> DomainEvents { get; } = [];

    public static Transaction Create(Guid tenantId, Guid bankAccountId, long amountMinor, DateOnly date, string type, DateTimeOffset now)
    {
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BankAccountId = bankAccountId,
            AmountMinor = amountMinor,
            Date = date,
            Type = type,
            Status = "active",
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now,
        };
        transaction.DomainEvents.Add(new TransactionRecorded(transaction.Id, transaction.TenantId, transaction.BankAccountId, transaction.AmountMinor, transaction.Date, transaction.Type, transaction.Status, now));
        return transaction;
    }

    public bool TryUpdate(int expectedVersion, Guid? bankAccountId, long? amountMinor, DateOnly? date, string? type, DateTimeOffset now)
    {
        if (Status != "active" || Version != expectedVersion) return false;

        BankAccountId = bankAccountId ?? BankAccountId;
        AmountMinor = amountMinor ?? AmountMinor;
        Date = date ?? Date;
        Type = type ?? Type;
        Version++;
        UpdatedAt = now;
        DomainEvents.Add(new TransactionUpdated(Id, TenantId, BankAccountId, AmountMinor, Date, Type, Status, now));
        return true;
    }

    public bool TryArchive(int expectedVersion, DateTimeOffset now)
    {
        if (Status != "active" || Version != expectedVersion) return false;

        Status = "archived";
        Version++;
        UpdatedAt = now;
        DomainEvents.Add(new TransactionArchived(Id, TenantId, now));
        return true;
    }

    public IReadOnlyCollection<object> GetDomainEvents() => DomainEvents;
    public void ClearDomainEvents() => DomainEvents.Clear();
}
