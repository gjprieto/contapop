namespace Contapop.Ledger.Service.Domain.Events;

public sealed record TransactionRecorded(Guid TransactionId, Guid TenantId, Guid BankAccountId, long AmountMinor, DateOnly Date, string Type, string? Description, string Status, DateTimeOffset OccurredAt);
public sealed record TransactionUpdated(Guid TransactionId, Guid TenantId, Guid BankAccountId, long AmountMinor, DateOnly Date, string Type, string? Description, string Status, DateTimeOffset OccurredAt);
public sealed record TransactionArchived(Guid TransactionId, Guid TenantId, DateTimeOffset OccurredAt);
