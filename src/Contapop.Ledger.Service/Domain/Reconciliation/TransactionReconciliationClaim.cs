namespace Contapop.Ledger.Service.Domain.Reconciliation;

public sealed class TransactionReconciliationClaim
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid TransactionId { get; private set; }
    public string DependentType { get; private set; } = null!;
    public Guid DependentId { get; private set; }
    public string Status { get; private set; } = null!;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ConfirmedAt { get; private set; }
    public DateTimeOffset? ReleasedAt { get; private set; }
    public long Version { get; private set; }

    public static TransactionReconciliationClaim Reserve(Guid tenantId, Guid transactionId, string dependentType, Guid dependentId, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, TransactionId = transactionId, DependentType = dependentType,
        DependentId = dependentId, Status = "reserved", ExpiresAt = now.AddMinutes(15), CreatedAt = now, Version = 1,
    };

    public bool TryConfirm(int version, DateTimeOffset now)
    {
        if (Status != "reserved" || Version != version || ExpiresAt <= now) return false;
        Status = "confirmed"; ConfirmedAt = now; Version++; return true;
    }

    public bool TryRelease(int? version, DateTimeOffset now)
    {
        if (Status == "released") return true;
        if (version is not null && Version != version) return false;
        if (Status == "confirmed" && DependentType == "payment") return false;
        Status = "released"; ReleasedAt = now; Version++; return true;
    }

    public bool Matches(Guid transactionId, string dependentType, Guid dependentId) =>
        TransactionId == transactionId && DependentType == dependentType && DependentId == dependentId;
}
