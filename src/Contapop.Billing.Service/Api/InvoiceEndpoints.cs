using System.Security.Claims;
using Contapop.Billing.Service.Application.Commands;
using Contapop.Billing.Service.Application.Attachments;
using Contapop.Billing.Service.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Contapop.Billing.Service.Api;

public static class InvoiceEndpoints
{
    public static IEndpointRouteBuilder MapInvoiceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var invoices = endpoints.MapGroup("/api/v1/invoices").RequireAuthorization("account-owner");
        invoices.MapPost("", CreateAsync);
        invoices.MapPatch("/{invoiceId:guid}", UpdateDraftAsync);
        invoices.MapPost("/{invoiceId:guid}/issue", IssueAsync);
        invoices.MapPost("/{invoiceId:guid}/void", VoidAsync);
        invoices.MapPost("/{invoiceId:guid}/archive", ArchiveAsync);
        invoices.MapDelete("/{invoiceId:guid}", DeleteDraftAsync);
        invoices.MapPut("/{invoiceId:guid}/attachment", AttachAsync).DisableAntiforgery();
        invoices.MapDelete("/{invoiceId:guid}/attachment", RemoveAttachmentAsync);
        invoices.MapGet("/{invoiceId:guid}/attachment", GetAttachmentAsync);
        invoices.MapGet("/{invoiceId:guid}", GetByIdAsync);
        invoices.MapGet("", ListAsync);
        return endpoints;
    }

    private static async Task<IResult> CreateAsync(CreateInvoiceRequest request, HttpContext context, InvoiceCommandHandler handler, CancellationToken cancellationToken)
    {
        if (!TryGetTenant(context, out var tenantId)) return Results.Unauthorized();
        if (!TryGetIdempotencyKey(context, out var key)) return MissingIdempotencyKey();
        var errors = ValidateCreate(request);
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var result = await handler.CreateAsync(new(tenantId, key, request.ProjectId, request.CounterpartyId, request.Direction, request.Type, request.Lines.Select(line => new CreateInvoiceLineCommand(line.Description.Trim(), line.Quantity, line.UnitPriceMinor, line.TaxRate)).ToArray(), request.Date, request.DueDate), cancellationToken);
        return result.IsProjectUnavailable ? Unprocessable("Project is unavailable.")
            : result.IsCounterpartyUnavailable ? Unprocessable("Counterparty is unavailable.")
            : Results.Created($"/api/v1/invoices/{result.Value!.InvoiceId}", result.Value);
    }

    private static async Task<IResult> IssueAsync(Guid invoiceId, HttpContext context, InvoiceCommandHandler handler, CancellationToken cancellationToken)
    {
        if (!TryGetTenant(context, out var tenantId)) return Results.Unauthorized();
        if (!TryGetIdempotencyKey(context, out var key)) return MissingIdempotencyKey();
        if (!TryGetExpectedVersion(context, out var version)) return MissingVersion();
        var result = await handler.IssueAsync(new(tenantId, key, invoiceId, version), cancellationToken);
        return result.IsNotFound ? Results.NotFound() : result.IsConflict ? Conflict("Only draft invoices can be issued.") : Results.Ok(result.Value);
    }

    private static async Task<IResult> UpdateDraftAsync(Guid invoiceId, UpdateDraftInvoiceRequest request, HttpContext context, InvoiceCommandHandler handler, BillingDbContext database, CancellationToken cancellationToken)
    {
        if (!TryGetTenant(context, out var tenantId)) return Results.Unauthorized();
        if (!TryGetIdempotencyKey(context, out var key)) return MissingIdempotencyKey();
        if (!TryGetExpectedVersion(context, out var version)) return MissingVersion();
        var errors = ValidateLinesAndDates(request.Lines, request.Date, request.DueDate);
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var result = await handler.UpdateDraftAsync(new(tenantId, key, invoiceId, version, request.Lines.Select(line => new CreateInvoiceLineCommand(line.Description.Trim(), line.Quantity, line.UnitPriceMinor, line.TaxRate)).ToArray(), request.Date, request.DueDate), cancellationToken);
        if (result.IsNotFound) return Results.NotFound();
        if (result.IsConflict) return Conflict("Only draft invoices at the current version can be updated.");
        return await GetByIdAsync(invoiceId, context, database, cancellationToken);
    }

    private static async Task<IResult> VoidAsync(Guid invoiceId, HttpContext context, InvoiceCommandHandler handler, CancellationToken cancellationToken)
    {
        if (!TryGetTenant(context, out var tenantId)) return Results.Unauthorized();
        if (!TryGetIdempotencyKey(context, out var key)) return MissingIdempotencyKey();
        if (!TryGetExpectedVersion(context, out var version)) return MissingVersion();
        var result = await handler.VoidAsync(new(tenantId, key, invoiceId, version), cancellationToken);
        return result.IsNotFound ? Results.NotFound() : result.IsConflict ? Conflict("Only draft invoices can be voided.") : Results.Ok(result.Value);
    }

    private static async Task<IResult> ArchiveAsync(Guid invoiceId, HttpContext context, InvoiceCommandHandler handler, CancellationToken cancellationToken)
    {
        if (!TryGetTenant(context, out var tenantId)) return Results.Unauthorized();
        if (!TryGetIdempotencyKey(context, out var key)) return MissingIdempotencyKey();
        if (!TryGetExpectedVersion(context, out var version)) return MissingVersion();
        var result = await handler.ArchiveAsync(new(tenantId, key, invoiceId, version), cancellationToken);
        return result.IsNotFound ? Results.NotFound() : result.IsConflict ? Conflict("Only unpaid invoices without payments can be archived.") : Results.Ok(result.Value);
    }

    private static async Task<IResult> DeleteDraftAsync(Guid invoiceId, HttpContext context, InvoiceCommandHandler handler, CancellationToken cancellationToken)
    {
        if (!TryGetTenant(context, out var tenantId)) return Results.Unauthorized();
        if (!TryGetIdempotencyKey(context, out var key)) return MissingIdempotencyKey();
        if (!TryGetExpectedVersion(context, out var version)) return MissingVersion();
        var result = await handler.DeleteDraftAsync(new(tenantId, key, invoiceId, version), cancellationToken);
        return result.IsNotFound ? Results.NotFound() : result.IsConflict ? Conflict("Only draft invoices without payments can be deleted.") : Results.NoContent();
    }

    private static async Task<IResult> GetByIdAsync(Guid invoiceId, HttpContext context, BillingDbContext database, CancellationToken cancellationToken)
    {
        if (!TryGetTenant(context, out var tenantId)) return Results.Unauthorized();
        var invoice = await database.Invoices.AsNoTracking().Where(item => item.Id == invoiceId && item.TenantId == tenantId)
            .Join(database.Counterparties.AsNoTracking(), invoice => invoice.CounterpartyId, counterparty => counterparty.Id, (invoice, counterparty) => new InvoiceDetailsResponse(invoice.Id, invoice.ProjectId, invoice.CounterpartyId, counterparty.Name, invoice.Direction, invoice.Type, invoice.Status, (invoice.Status == "issued" || invoice.Status == "overdue" || invoice.Status == "void") && !database.Payments.Any(payment => payment.TenantId == tenantId && payment.InvoiceId == invoice.Id), invoice.Status == "draft" && !database.Payments.Any(payment => payment.TenantId == tenantId && payment.InvoiceId == invoice.Id), invoice.NetAmountMinor, invoice.TaxAmountMinor, invoice.TotalAmountMinor, invoice.Date, invoice.DueDate, invoice.CreatedAt, invoice.UpdatedAt, (int)invoice.Version))
            .SingleOrDefaultAsync(cancellationToken);
        if (invoice is null) return Results.NotFound();
        var lines = await database.InvoiceLines.AsNoTracking().Where(line => line.InvoiceId == invoiceId).Select(line => new InvoiceLineResponse(line.Id, line.Description, line.Quantity, line.UnitPriceMinor, line.TaxRate, line.NetAmountMinor, line.TaxAmountMinor, line.TotalAmountMinor)).ToListAsync(cancellationToken);
        var payments = await database.Payments.AsNoTracking().Where(payment => payment.InvoiceId == invoiceId).OrderByDescending(payment => payment.Date).Select(payment => new InvoicePaymentResponse(payment.Id, payment.AmountMinor, payment.Date, payment.PaymentMethod, payment.ReconciledTransactionId)).ToListAsync(cancellationToken);
        var attachment = await database.InvoiceAttachments.AsNoTracking().Where(item => item.InvoiceId == invoiceId && item.TenantId == tenantId).Select(item => new InvoiceAttachmentResponse(item.Id, item.InvoiceId, item.OriginalFileName, item.ContentType, item.SizeBytes, item.CreatedAt, item.UpdatedAt)).SingleOrDefaultAsync(cancellationToken);
        return Results.Ok(invoice with { Lines = lines, Payments = payments, Attachment = attachment });
    }

    private static async Task<IResult> ListAsync(string? status, string? direction, string? type, DateOnly? dateFrom, DateOnly? dateTo, long? totalAmountMinMinor, long? totalAmountMaxMinor, string? search, string? sort, int? page, int? pageSize, HttpContext context, BillingDbContext database, CancellationToken cancellationToken)
    {
        if (!TryGetTenant(context, out var tenantId)) return Results.Unauthorized();
        if (status is not null && status is not ("draft" or "issued" or "paid" or "overdue" or "void" or "archived") || direction is not null && direction is not ("incoming" or "outgoing") || type is not null && type is not ("service" or "product") || dateFrom > dateTo || totalAmountMinMinor is < 0 || totalAmountMaxMinor is < 0 || totalAmountMinMinor > totalAmountMaxMinor || sort is not null && sort is not ("date:asc" or "date:desc" or "amount:asc" or "amount:desc")) return Results.ValidationProblem(new Dictionary<string, string[]> { ["query"] = ["One or more query parameters are invalid."] });
        var actualPage = Math.Max(page ?? 1, 1);
        var actualPageSize = Math.Clamp(pageSize ?? 25, 1, 100);
        var query = from invoice in database.Invoices.AsNoTracking()
                    join counterparty in database.Counterparties.AsNoTracking() on invoice.CounterpartyId equals counterparty.Id
                    where invoice.TenantId == tenantId
                    select new { Invoice = invoice, CounterpartyName = counterparty.Name };
        if (status is not null) query = query.Where(item => item.Invoice.Status == status);
        else query = query.Where(item => item.Invoice.Status != "archived");
        if (direction is not null) query = query.Where(item => item.Invoice.Direction == direction);
        if (type is not null) query = query.Where(item => item.Invoice.Type == type);
        if (dateFrom is not null) query = query.Where(item => item.Invoice.Date >= dateFrom);
        if (dateTo is not null) query = query.Where(item => item.Invoice.Date <= dateTo);
        if (totalAmountMinMinor is not null) query = query.Where(item => item.Invoice.TotalAmountMinor >= totalAmountMinMinor);
        if (totalAmountMaxMinor is not null) query = query.Where(item => item.Invoice.TotalAmountMinor <= totalAmountMaxMinor);
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim(); query = query.Where(item => item.CounterpartyName.Contains(term)); }
        var total = await query.CountAsync(cancellationToken);
        var ordered = sort switch
        {
            "date:asc" => query.OrderBy(item => item.Invoice.Date).ThenBy(item => item.Invoice.Id),
            "amount:asc" => query.OrderBy(item => item.Invoice.TotalAmountMinor).ThenBy(item => item.Invoice.Id),
            "amount:desc" => query.OrderByDescending(item => item.Invoice.TotalAmountMinor).ThenByDescending(item => item.Invoice.Id),
            _ => query.OrderByDescending(item => item.Invoice.Date).ThenByDescending(item => item.Invoice.Id),
        };
        var items = await ordered.Skip((actualPage - 1) * actualPageSize).Take(actualPageSize)
            .Select(item => new InvoiceListItem(item.Invoice.Id, item.Invoice.CounterpartyId, item.CounterpartyName, item.Invoice.Direction, item.Invoice.Type, item.Invoice.Status, (item.Invoice.Status == "issued" || item.Invoice.Status == "overdue" || item.Invoice.Status == "void") && !database.Payments.Any(payment => payment.TenantId == tenantId && payment.InvoiceId == item.Invoice.Id), item.Invoice.Status == "draft" && !database.Payments.Any(payment => payment.TenantId == tenantId && payment.InvoiceId == item.Invoice.Id), item.Invoice.NetAmountMinor, item.Invoice.TaxAmountMinor, item.Invoice.TotalAmountMinor, item.Invoice.Date, item.Invoice.DueDate, (int)item.Invoice.Version))
            .ToListAsync(cancellationToken);
        return Results.Ok(new InvoicePagedResponse(items, actualPage, actualPageSize, total));
    }

    private static Dictionary<string, string[]> ValidateCreate(CreateInvoiceRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.ProjectId == Guid.Empty) errors["projectId"] = ["Project must be a non-empty GUID."];
        if (request.CounterpartyId == Guid.Empty) errors["counterpartyId"] = ["Counterparty must be a non-empty GUID."];
        if (request.Direction is not ("incoming" or "outgoing")) errors["direction"] = ["Direction must be incoming or outgoing."];
        if (request.Type is not ("service" or "product")) errors["type"] = ["Type must be service or product."];
        foreach (var error in ValidateLinesAndDates(request.Lines, request.Date, request.DueDate)) errors[error.Key] = error.Value;
        return errors;
    }

    private static Dictionary<string, string[]> ValidateLinesAndDates(IReadOnlyList<CreateInvoiceLineRequest> lines, DateOnly date, DateOnly dueDate)
    {
        var errors = new Dictionary<string, string[]>();
        if (lines.Count == 0) errors["lines"] = ["At least one invoice line is required."];
        if (lines.Any(line => string.IsNullOrWhiteSpace(line.Description) || line.Description.Length > 500 || line.Quantity <= 0 || line.UnitPriceMinor <= 0 || line.TaxRate is < 0m or > 1m)) errors["lines"] = ["Each line requires a description, positive quantity and unit price, and a VAT rate between 0 and 1."];
        if (dueDate < date) errors["dueDate"] = ["Due date cannot be before the invoice date."];
        return errors;
    }

    private static async Task<IResult> AttachAsync(Guid invoiceId, IFormFile? file, HttpContext context, InvoiceAttachmentService attachments, CancellationToken cancellationToken)
    {
        if (!TryGetTenant(context, out var tenantId)) return Results.Unauthorized();
        if (file is null) return Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = ["An attachment file is required."] });
        var result = await attachments.AttachAsync(tenantId, invoiceId, file, cancellationToken);
        if (result.IsNotFound) return Results.NotFound();
        if (result.IsConflict) return Conflict("Archived invoices cannot be modified.");
        if (result.Error is not null)
        {
            return file.Length > 10_485_760
                ? Results.Problem(statusCode: StatusCodes.Status413PayloadTooLarge, title: "Attachment too large", detail: result.Error)
                : Unprocessable(result.Error);
        }
        return result.IsNew ? Results.Created($"/api/v1/invoices/{invoiceId}/attachment", result.Attachment) : Results.Ok(result.Attachment);
    }

    private static async Task<IResult> RemoveAttachmentAsync(Guid invoiceId, HttpContext context, InvoiceAttachmentService attachments, CancellationToken cancellationToken)
    {
        if (!TryGetTenant(context, out var tenantId)) return Results.Unauthorized();
        await attachments.RemoveAsync(tenantId, invoiceId, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GetAttachmentAsync(Guid invoiceId, HttpContext context, InvoiceAttachmentService attachments, CancellationToken cancellationToken)
    {
        if (!TryGetTenant(context, out var tenantId)) return Results.Unauthorized();
        var attachment = await attachments.GetAsync(tenantId, invoiceId, cancellationToken);
        return attachment is null ? Results.NotFound() : Results.File(attachment.Content, attachment.ContentType, attachment.FileName, enableRangeProcessing: true);
    }

    private static bool TryGetTenant(HttpContext context, out Guid tenantId) => Guid.TryParse(context.User.FindFirstValue("tenant_id"), out tenantId);
    private static bool TryGetIdempotencyKey(HttpContext context, out string key) { key = context.Request.Headers["Idempotency-Key"].ToString(); return Guid.TryParse(key, out _); }
    private static bool TryGetExpectedVersion(HttpContext context, out int version) { var value = context.Request.Headers.IfMatch.ToString(); version = 0; var valid = value.Length >= 3 && value[0] == '"' && value[^1] == '"' && int.TryParse(value[1..^1], out version) && version > 0; if (!valid) version = 0; return valid; }
    private static IResult MissingIdempotencyKey() => Results.ValidationProblem(new Dictionary<string, string[]> { ["Idempotency-Key"] = ["A GUID idempotency key is required."] });
    private static IResult MissingVersion() => Results.ValidationProblem(new Dictionary<string, string[]> { ["If-Match"] = ["A quoted current resource version is required."] });
    private static IResult Unprocessable(string detail) => Results.Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: "Validation failed", detail: detail);
    private static IResult Conflict(string detail) => Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflict", detail: detail);
}

public sealed record CreateInvoiceRequest(Guid ProjectId, Guid CounterpartyId, string Direction, string Type, IReadOnlyList<CreateInvoiceLineRequest> Lines, DateOnly Date, DateOnly DueDate);
public sealed record CreateInvoiceLineRequest(string Description, int Quantity, long UnitPriceMinor, decimal TaxRate);
public sealed record UpdateDraftInvoiceRequest(IReadOnlyList<CreateInvoiceLineRequest> Lines, DateOnly Date, DateOnly DueDate);
public sealed record InvoiceListItem(Guid InvoiceId, Guid CounterpartyId, string CounterpartyName, string Direction, string Type, string Status, bool CanArchive, bool CanDelete, long NetAmountMinor, long TaxAmountMinor, long TotalAmountMinor, DateOnly Date, DateOnly DueDate, int Version);
public sealed record InvoicePagedResponse(IReadOnlyList<InvoiceListItem> Items, int Page, int PageSize, int TotalCount);
public sealed record InvoiceDetailsResponse(Guid InvoiceId, Guid ProjectId, Guid CounterpartyId, string CounterpartyName, string Direction, string Type, string Status, bool CanArchive, bool CanDelete, long NetAmountMinor, long TaxAmountMinor, long TotalAmountMinor, DateOnly Date, DateOnly DueDate, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, int Version)
{ public IReadOnlyList<InvoiceLineResponse> Lines { get; init; } = []; public IReadOnlyList<InvoicePaymentResponse> Payments { get; init; } = []; public InvoiceAttachmentResponse? Attachment { get; init; } }
public sealed record InvoiceLineResponse(Guid InvoiceLineId, string Description, int Quantity, long UnitPriceMinor, decimal TaxRate, long NetAmountMinor, long TaxAmountMinor, long TotalAmountMinor);
public sealed record InvoicePaymentResponse(Guid PaymentId, long AmountMinor, DateOnly Date, string PaymentMethod, Guid? ReconciledTransactionId);
