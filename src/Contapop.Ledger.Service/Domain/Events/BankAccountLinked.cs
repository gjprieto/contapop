namespace Contapop.Ledger.Service.Domain.Events;

public sealed record BankAccountLinked(
    Guid BankAccountId,
    Guid TenantId,
    Guid ProjectId,
    string BankName,
    string Status,
    DateTimeOffset OccurredAt);
