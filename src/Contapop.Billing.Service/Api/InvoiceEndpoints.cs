using System.Security.Claims;
using Contapop.Billing.Service.Application.Commands;
using Contapop.Billing.Service.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Contapop.Billing.Service.Api;

public static class InvoiceEndpoints
{
    public static IEndpointRouteBuilder MapInvoiceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var invoices = endpoints.MapGroup("/api/v1/invoices").RequireAuthorization("account-owner");
        invoices.MapPost("", CreateAsync);
        invoices.MapPost("/{invoiceId:guid}/issue", IssueAsync);
        invoices.MapPost("/{invoiceId:guid}/void", VoidAsync);
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

    private static async Task<IResult> VoidAsync(Guid invoiceId, HttpContext context, InvoiceCommandHandler handler, CancellationToken cancellationToken)
    {
        if (!TryGetTenant(context, out var tenantId)) return Results.Unauthorized();
        if (!TryGetIdempotencyKey(context, out var key)) return MissingIdempotencyKey();
        if (!TryGetExpectedVersion(context, out var version)) return MissingVersion();
        var result = await handler.VoidAsync(new(tenantId, key, invoiceId, version), cancellationToken);
        return result.IsNotFound ? Results.NotFound() : result.IsConflict ? Conflict("Only draft invoices can be voided.") : Results.Ok(result.Value);
    }

    private static async Task<IResult> GetByIdAsync(Guid invoiceId, HttpContext context, BillingDbContext database, CancellationToken cancellationToken)
    {
        if (!TryGetTenant(context, out var tenantId)) return Results.Unauthorized();
        var invoice = await database.Invoices.AsNoTracking().Where(item => item.Id == invoiceId && item.TenantId == tenantId)
            .Join(database.Counterparties.AsNoTracking(), invoice => invoice.CounterpartyId, counterparty => counterparty.Id, (invoice, counterparty) => new InvoiceDetailsResponse(invoice.Id, invoice.ProjectId, invoice.CounterpartyId, counterparty.Name, invoice.Direction, invoice.Type, invoice.Status, invoice.NetAmountMinor, invoice.TaxAmountMinor, invoice.TotalAmountMinor, invoice.Date, invoice.DueDate, invoice.CreatedAt, invoice.UpdatedAt, (int)invoice.Version))
            .SingleOrDefaultAsync(cancellationToken);
        if (invoice is null) return Results.NotFound();
        var lines = await database.InvoiceLines.AsNoTracking().Where(line => line.InvoiceId == invoiceId).Select(line => new InvoiceLineResponse(line.Id, line.Description, line.Quantity, line.UnitPriceMinor, line.TaxRate, line.NetAmountMinor, line.TaxAmountMinor, line.TotalAmountMinor)).ToListAsync(cancellationToken);
        var payments = await database.Payments.AsNoTracking().Where(payment => payment.InvoiceId == invoiceId).OrderByDescending(payment => payment.Date).Select(payment => new InvoicePaymentResponse(payment.Id, payment.AmountMinor, payment.Date, payment.PaymentMethod, payment.ReconciledTransactionId)).ToListAsync(cancellationToken);
        return Results.Ok(invoice with { Lines = lines, Payments = payments });
    }

    private static async Task<IResult> ListAsync(string? status, string? direction, string? search, string? sort, int? page, int? pageSize, HttpContext context, BillingDbContext database, CancellationToken cancellationToken)
    {
        if (!TryGetTenant(context, out var tenantId)) return Results.Unauthorized();
        if (status is not null && status is not ("draft" or "issued" or "paid" or "overdue" or "void") || direction is not null && direction is not ("incoming" or "outgoing") || sort is not null && sort is not ("date:asc" or "date:desc" or "amount:asc" or "amount:desc")) return Results.ValidationProblem(new Dictionary<string, string[]> { ["query"] = ["One or more query parameters are invalid."] });
        var actualPage = Math.Max(page ?? 1, 1);
        var actualPageSize = Math.Clamp(pageSize ?? 25, 1, 100);
        var query = from invoice in database.Invoices.AsNoTracking()
                    join counterparty in database.Counterparties.AsNoTracking() on invoice.CounterpartyId equals counterparty.Id
                    where invoice.TenantId == tenantId
                    select new InvoiceListItem(invoice.Id, invoice.CounterpartyId, counterparty.Name, invoice.Direction, invoice.Type, invoice.Status, invoice.NetAmountMinor, invoice.TaxAmountMinor, invoice.TotalAmountMinor, invoice.Date, invoice.DueDate);
        if (status is not null) query = query.Where(item => item.Status == status);
        if (direction is not null) query = query.Where(item => item.Direction == direction);
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim(); query = query.Where(item => item.CounterpartyName.Contains(term)); }
        var total = await query.CountAsync(cancellationToken);
        var ordered = sort switch
        {
            "date:asc" => query.OrderBy(item => item.Date).ThenBy(item => item.InvoiceId),
            "amount:asc" => query.OrderBy(item => item.TotalAmountMinor).ThenBy(item => item.InvoiceId),
            "amount:desc" => query.OrderByDescending(item => item.TotalAmountMinor).ThenByDescending(item => item.InvoiceId),
            _ => query.OrderByDescending(item => item.Date).ThenByDescending(item => item.InvoiceId),
        };
        var items = await ordered.Skip((actualPage - 1) * actualPageSize).Take(actualPageSize).ToListAsync(cancellationToken);
        return Results.Ok(new InvoicePagedResponse(items, actualPage, actualPageSize, total));
    }

    private static Dictionary<string, string[]> ValidateCreate(CreateInvoiceRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.ProjectId == Guid.Empty) errors["projectId"] = ["Project must be a non-empty GUID."];
        if (request.CounterpartyId == Guid.Empty) errors["counterpartyId"] = ["Counterparty must be a non-empty GUID."];
        if (request.Direction is not ("incoming" or "outgoing")) errors["direction"] = ["Direction must be incoming or outgoing."];
        if (request.Type is not ("service" or "product")) errors["type"] = ["Type must be service or product."];
        if (request.Lines.Count == 0) errors["lines"] = ["At least one invoice line is required."];
        if (request.Lines.Any(line => string.IsNullOrWhiteSpace(line.Description) || line.Description.Length > 500 || line.Quantity <= 0 || line.UnitPriceMinor <= 0 || line.TaxRate is < 0m or > 1m)) errors["lines"] = ["Each line requires a description, positive quantity and unit price, and a VAT rate between 0 and 1."];
        if (request.DueDate < request.Date) errors["dueDate"] = ["Due date cannot be before the invoice date."];
        return errors;
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
public sealed record InvoiceListItem(Guid InvoiceId, Guid CounterpartyId, string CounterpartyName, string Direction, string Type, string Status, long NetAmountMinor, long TaxAmountMinor, long TotalAmountMinor, DateOnly Date, DateOnly DueDate);
public sealed record InvoicePagedResponse(IReadOnlyList<InvoiceListItem> Items, int Page, int PageSize, int TotalCount);
public sealed record InvoiceDetailsResponse(Guid InvoiceId, Guid ProjectId, Guid CounterpartyId, string CounterpartyName, string Direction, string Type, string Status, long NetAmountMinor, long TaxAmountMinor, long TotalAmountMinor, DateOnly Date, DateOnly DueDate, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, int Version)
{ public IReadOnlyList<InvoiceLineResponse> Lines { get; init; } = []; public IReadOnlyList<InvoicePaymentResponse> Payments { get; init; } = []; }
public sealed record InvoiceLineResponse(Guid InvoiceLineId, string Description, int Quantity, long UnitPriceMinor, decimal TaxRate, long NetAmountMinor, long TaxAmountMinor, long TotalAmountMinor);
public sealed record InvoicePaymentResponse(Guid PaymentId, long AmountMinor, DateOnly Date, string PaymentMethod, Guid? ReconciledTransactionId);
