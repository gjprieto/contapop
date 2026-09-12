namespace Contapop.Billing.Service.Application.Commands;

public sealed record CreateInvoiceCommand(Guid TenantId, string IdempotencyKey, Guid ProjectId, Guid CounterpartyId, string Direction, string Type, IReadOnlyList<CreateInvoiceLineCommand> Lines, DateOnly Date, DateOnly DueDate)
{
    public CreateInvoiceCommand(Guid tenantId, string idempotencyKey, Guid projectId, Guid counterpartyId, string direction, long netAmountMinor, decimal taxRate, DateOnly date, DateOnly dueDate)
        : this(tenantId, idempotencyKey, projectId, counterpartyId, direction, "service", [new("Invoice amount", 1, netAmountMinor, taxRate)], date, dueDate)
    {
    }
}
public sealed record CreateInvoiceLineCommand(string Description, int Quantity, long UnitPriceMinor, decimal TaxRate);
public sealed record UpdateDraftInvoiceCommand(Guid TenantId, string IdempotencyKey, Guid InvoiceId, int ExpectedVersion, IReadOnlyList<CreateInvoiceLineCommand> Lines, DateOnly Date, DateOnly DueDate);
public sealed record IssueInvoiceCommand(Guid TenantId, string IdempotencyKey, Guid InvoiceId, int ExpectedVersion);
public sealed record VoidInvoiceCommand(Guid TenantId, string IdempotencyKey, Guid InvoiceId, int ExpectedVersion);
public sealed record ArchiveInvoiceCommand(Guid TenantId, string IdempotencyKey, Guid InvoiceId, int ExpectedVersion);
public sealed record DeleteDraftInvoiceCommand(Guid TenantId, string IdempotencyKey, Guid InvoiceId, int ExpectedVersion);
