namespace Contapop.Billing.Service.Application.Commands;

public sealed record RecordPaymentCommand(Guid TenantId, string IdempotencyKey, Guid InvoiceId, long AmountMinor, DateOnly Date, string PaymentMethod);
public sealed record ReconcilePaymentCommand(Guid TenantId, string IdempotencyKey, Guid PaymentId, Guid TransactionId, Guid ReconciliationClaimId, int ExpectedVersion);
