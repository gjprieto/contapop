using System.Text.Json;
using Contapop.Billing.Service.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Contapop.Billing.Service.Application.Commands;

public sealed class InvoiceCommandHandler(BillingDbContext database)
{
    public async Task<InvoiceCommandResult<CreatedInvoiceResponse>> CreateAsync(CreateInvoiceCommand command, CancellationToken cancellationToken)
    {
        var replay = await GetReplayAsync<CreatedInvoiceResponse>(command.TenantId, "create-invoice", command.IdempotencyKey, cancellationToken);
        if (replay is not null) return InvoiceCommandResult<CreatedInvoiceResponse>.Success(replay);
        if (!await database.ProjectReplicas.AnyAsync(project => project.ProjectId == command.ProjectId && project.TenantId == command.TenantId && project.Status == "active", cancellationToken)) return InvoiceCommandResult<CreatedInvoiceResponse>.ProjectUnavailable();
        if (!await database.Counterparties.AnyAsync(counterparty => counterparty.Id == command.CounterpartyId && counterparty.TenantId == command.TenantId && counterparty.Status == "active", cancellationToken)) return InvoiceCommandResult<CreatedInvoiceResponse>.CounterpartyUnavailable();

        var invoice = Invoice.Create(command.TenantId, command.ProjectId, command.CounterpartyId, command.Direction, command.Type, command.Lines.Select(line => new CreateInvoiceLine(line.Description, line.Quantity, line.UnitPriceMinor, line.TaxRate)).ToArray(), command.Date, command.DueDate, DateTimeOffset.UtcNow);
        var result = new CreatedInvoiceResponse(invoice.Id, invoice.Status, invoice.NetAmountMinor, invoice.TaxAmountMinor, invoice.TotalAmountMinor, invoice.CreatedAt, (int)invoice.Version);
        database.Invoices.Add(invoice);
        database.IdempotencyRecords.Add(IdempotencyRecord.Create(command.TenantId, "create-invoice", command.IdempotencyKey, JsonSerializer.Serialize(result), invoice.CreatedAt));
        await database.SaveChangesAsync(cancellationToken);
        return InvoiceCommandResult<CreatedInvoiceResponse>.Success(result);
    }

    public async Task<InvoiceCommandResult<InvoiceStatusResponse>> IssueAsync(IssueInvoiceCommand command, CancellationToken cancellationToken)
    {
        var replay = await GetReplayAsync<InvoiceStatusResponse>(command.TenantId, "issue-invoice", command.IdempotencyKey, cancellationToken);
        if (replay is not null) return InvoiceCommandResult<InvoiceStatusResponse>.Success(replay);
        var invoice = await database.Invoices.SingleOrDefaultAsync(item => item.Id == command.InvoiceId && item.TenantId == command.TenantId, cancellationToken);
        if (invoice is null) return InvoiceCommandResult<InvoiceStatusResponse>.NotFound();
        var now = DateTimeOffset.UtcNow;
        if (!invoice.TryIssue(command.ExpectedVersion, now)) return InvoiceCommandResult<InvoiceStatusResponse>.Conflict();
        var result = new InvoiceStatusResponse(invoice.Id, invoice.Status, now, (int)invoice.Version);
        database.OutboxMessages.Add(OutboxMessage.Create("billing.invoice-issued.v1", "Invoice", invoice.Id, invoice.Version, invoice.TenantId, now, JsonSerializer.Serialize(new { invoice_id = invoice.Id, project_id = invoice.ProjectId, counterparty_id = invoice.CounterpartyId, direction = invoice.Direction, type = invoice.Type, net_amount_minor = invoice.NetAmountMinor, tax_amount_minor = invoice.TaxAmountMinor, total_amount_minor = invoice.TotalAmountMinor, date = invoice.Date, due_date = invoice.DueDate, issued_at = now })));
        database.IdempotencyRecords.Add(IdempotencyRecord.Create(command.TenantId, "issue-invoice", command.IdempotencyKey, JsonSerializer.Serialize(result), now));
        await database.SaveChangesAsync(cancellationToken);
        return InvoiceCommandResult<InvoiceStatusResponse>.Success(result);
    }

    public async Task<InvoiceCommandResult<InvoiceStatusResponse>> VoidAsync(VoidInvoiceCommand command, CancellationToken cancellationToken)
    {
        var replay = await GetReplayAsync<InvoiceStatusResponse>(command.TenantId, "void-invoice", command.IdempotencyKey, cancellationToken);
        if (replay is not null) return InvoiceCommandResult<InvoiceStatusResponse>.Success(replay);
        var invoice = await database.Invoices.SingleOrDefaultAsync(item => item.Id == command.InvoiceId && item.TenantId == command.TenantId, cancellationToken);
        if (invoice is null) return InvoiceCommandResult<InvoiceStatusResponse>.NotFound();
        var now = DateTimeOffset.UtcNow;
        if (!invoice.TryVoid(command.ExpectedVersion, now)) return InvoiceCommandResult<InvoiceStatusResponse>.Conflict();
        var result = new InvoiceStatusResponse(invoice.Id, invoice.Status, now, (int)invoice.Version);
        database.IdempotencyRecords.Add(IdempotencyRecord.Create(command.TenantId, "void-invoice", command.IdempotencyKey, JsonSerializer.Serialize(result), now));
        await database.SaveChangesAsync(cancellationToken);
        return InvoiceCommandResult<InvoiceStatusResponse>.Success(result);
    }

    public async Task<InvoiceCommandResult<InvoiceStatusResponse>> ArchiveAsync(ArchiveInvoiceCommand command, CancellationToken cancellationToken)
    {
        var replay = await GetReplayAsync<InvoiceStatusResponse>(command.TenantId, "archive-invoice", command.IdempotencyKey, cancellationToken);
        if (replay is not null) return InvoiceCommandResult<InvoiceStatusResponse>.Success(replay);
        var invoice = await database.Invoices.SingleOrDefaultAsync(item => item.Id == command.InvoiceId && item.TenantId == command.TenantId, cancellationToken);
        if (invoice is null) return InvoiceCommandResult<InvoiceStatusResponse>.NotFound();
        if (invoice.Status == "paid" || await database.Payments.AnyAsync(payment => payment.TenantId == command.TenantId && payment.InvoiceId == command.InvoiceId, cancellationToken)) return InvoiceCommandResult<InvoiceStatusResponse>.Conflict();

        var previousStatus = invoice.Status;
        var now = DateTimeOffset.UtcNow;
        if (!invoice.TryArchive(command.ExpectedVersion, now)) return InvoiceCommandResult<InvoiceStatusResponse>.Conflict();
        var attachment = await database.InvoiceAttachments.SingleOrDefaultAsync(item => item.InvoiceId == invoice.Id, cancellationToken);
        if (attachment is not null)
        {
            database.InvoiceAttachments.Remove(attachment);
            database.AttachmentCleanups.Add(AttachmentCleanup.Create(invoice.TenantId, attachment.BlobName, now));
        }
        var result = new InvoiceStatusResponse(invoice.Id, invoice.Status, now, (int)invoice.Version);
        database.OutboxMessages.Add(OutboxMessage.Create("billing.invoice-archived.v1", "Invoice", invoice.Id, invoice.Version, invoice.TenantId, now, JsonSerializer.Serialize(new { invoice_id = invoice.Id, project_id = invoice.ProjectId, direction = invoice.Direction, previous_status = previousStatus, archived_at = now })));
        database.IdempotencyRecords.Add(IdempotencyRecord.Create(command.TenantId, "archive-invoice", command.IdempotencyKey, JsonSerializer.Serialize(result), now));
        await database.SaveChangesAsync(cancellationToken);
        return InvoiceCommandResult<InvoiceStatusResponse>.Success(result);
    }

    public async Task<InvoiceCommandResult<DeletedInvoiceResponse>> DeleteDraftAsync(DeleteDraftInvoiceCommand command, CancellationToken cancellationToken)
    {
        var replay = await GetReplayAsync<DeletedInvoiceResponse>(command.TenantId, "delete-draft-invoice", command.IdempotencyKey, cancellationToken);
        if (replay is not null) return InvoiceCommandResult<DeletedInvoiceResponse>.Success(replay);

        var invoice = await database.Invoices.SingleOrDefaultAsync(item => item.Id == command.InvoiceId && item.TenantId == command.TenantId, cancellationToken);
        if (invoice is null) return InvoiceCommandResult<DeletedInvoiceResponse>.NotFound();
        if (invoice.Status != "draft" || invoice.Version != command.ExpectedVersion || await database.Payments.AnyAsync(payment => payment.TenantId == command.TenantId && payment.InvoiceId == command.InvoiceId, cancellationToken)) return InvoiceCommandResult<DeletedInvoiceResponse>.Conflict();

        var now = DateTimeOffset.UtcNow;
        var attachment = await database.InvoiceAttachments.SingleOrDefaultAsync(item => item.InvoiceId == invoice.Id, cancellationToken);
        if (attachment is not null)
        {
            database.InvoiceAttachments.Remove(attachment);
            database.AttachmentCleanups.Add(AttachmentCleanup.Create(invoice.TenantId, attachment.BlobName, now));
        }
        database.Invoices.Remove(invoice);
        var result = new DeletedInvoiceResponse();
        database.IdempotencyRecords.Add(IdempotencyRecord.Create(command.TenantId, "delete-draft-invoice", command.IdempotencyKey, JsonSerializer.Serialize(result), now));
        await database.SaveChangesAsync(cancellationToken);
        return InvoiceCommandResult<DeletedInvoiceResponse>.Success(result);
    }

    private async Task<T?> GetReplayAsync<T>(Guid tenantId, string operation, string key, CancellationToken cancellationToken) where T : class
    {
        var result = await database.IdempotencyRecords.AsNoTracking().Where(record => record.TenantId == tenantId && record.Operation == operation && record.Key == key).Select(record => record.Result).SingleOrDefaultAsync(cancellationToken);
        return result is null ? null : JsonSerializer.Deserialize<T>(result);
    }
}

public sealed record CreatedInvoiceResponse(Guid InvoiceId, string Status, long NetAmountMinor, long TaxAmountMinor, long TotalAmountMinor, DateTimeOffset CreatedAt, int Version);
public sealed record InvoiceStatusResponse(Guid InvoiceId, string Status, DateTimeOffset UpdatedAt, int Version);
public sealed record DeletedInvoiceResponse;
public sealed class InvoiceCommandResult<T> where T : class
{
    public T? Value { get; private init; }
    public bool IsProjectUnavailable { get; private init; }
    public bool IsCounterpartyUnavailable { get; private init; }
    public bool IsNotFound { get; private init; }
    public bool IsConflict { get; private init; }
    public static InvoiceCommandResult<T> Success(T value) => new() { Value = value };
    public static InvoiceCommandResult<T> ProjectUnavailable() => new() { IsProjectUnavailable = true };
    public static InvoiceCommandResult<T> CounterpartyUnavailable() => new() { IsCounterpartyUnavailable = true };
    public static InvoiceCommandResult<T> NotFound() => new() { IsNotFound = true };
    public static InvoiceCommandResult<T> Conflict() => new() { IsConflict = true };
}
