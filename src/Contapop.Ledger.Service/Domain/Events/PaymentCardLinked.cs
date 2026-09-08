namespace Contapop.Ledger.Service.Domain.Events;

public sealed record PaymentCardLinked(
    Guid CardId,
    Guid TenantId,
    Guid ProjectId,
    string Label,
    DateTimeOffset OccurredAt);
