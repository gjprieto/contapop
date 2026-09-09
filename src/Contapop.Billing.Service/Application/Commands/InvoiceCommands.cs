namespace Contapop.Billing.Service.Application.Commands;

public sealed record CreateInvoiceCommand(Guid TenantId, string IdempotencyKey, Guid ProjectId, Guid CounterpartyId, string Direction, long NetAmountMinor, decimal TaxRate, DateOnly Date, DateOnly DueDate);
public sealed record IssueInvoiceCommand(Guid TenantId, string IdempotencyKey, Guid InvoiceId, int ExpectedVersion);
public sealed record VoidInvoiceCommand(Guid TenantId, string IdempotencyKey, Guid InvoiceId, int ExpectedVersion);
