namespace Contapop.Ledger.Service.Application.Commands;

public sealed record LinkBankAccountCommand(Guid TenantId, string IdempotencyKey, Guid ProjectId, string AccountNumber, string BankName);
public sealed record ArchiveBankAccountCommand(Guid TenantId, string IdempotencyKey, Guid BankAccountId, int ExpectedVersion);
public sealed record AddPaymentCardLabelCommand(Guid TenantId, string IdempotencyKey, Guid ProjectId, string Label, string CardholderName, DateOnly? ExpirationDate);
public sealed record RemovePaymentCardLabelCommand(Guid TenantId, string IdempotencyKey, Guid CardId, int ExpectedVersion);
