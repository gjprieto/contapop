namespace Contapop.Ledger.Service.Application.Commands;

public sealed record RecordTransactionCommand(Guid TenantId, string IdempotencyKey, Guid BankAccountId, long AmountMinor, DateOnly Date, string Type, string? Description = null);
public sealed record ImportTransactionRow(DateOnly Date, long AmountMinor, string Type, string? Description);
public sealed record ImportTransactionsCommand(Guid TenantId, string IdempotencyKey, Guid BankAccountId, IReadOnlyList<ImportTransactionRow> Rows);
public sealed record UpdateTransactionCommand(Guid TenantId, string IdempotencyKey, Guid TransactionId, int ExpectedVersion, Guid? BankAccountId, long? AmountMinor, DateOnly? Date, string? Type, string? Description = null);
public sealed record ArchiveTransactionCommand(Guid TenantId, string IdempotencyKey, Guid TransactionId, int ExpectedVersion);
