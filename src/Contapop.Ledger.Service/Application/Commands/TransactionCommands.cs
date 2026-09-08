namespace Contapop.Ledger.Service.Application.Commands;

public sealed record RecordTransactionCommand(Guid TenantId, string IdempotencyKey, Guid BankAccountId, long AmountMinor, DateOnly Date, string Type);
public sealed record UpdateTransactionCommand(Guid TenantId, string IdempotencyKey, Guid TransactionId, int ExpectedVersion, Guid? BankAccountId, long? AmountMinor, DateOnly? Date, string? Type);
public sealed record ArchiveTransactionCommand(Guid TenantId, string IdempotencyKey, Guid TransactionId, int ExpectedVersion);
