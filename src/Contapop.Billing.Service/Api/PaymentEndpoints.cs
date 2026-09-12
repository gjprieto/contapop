using System.Security.Claims;
using Contapop.Billing.Service.Application.Commands;
using Contapop.Billing.Service.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Contapop.Billing.Service.Api;

public static class PaymentEndpoints
{
    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var payments = endpoints.MapGroup("/api/v1/payments").RequireAuthorization("account-owner");
        payments.MapPost("", RecordAsync);
        payments.MapPost("/{paymentId:guid}/reconcile", ReconcileAsync);
        payments.MapGet("", ListAsync);
        return endpoints;
    }

    private static async Task<IResult> RecordAsync(RecordPaymentRequest request, HttpContext context, PaymentCommandHandler handler, CancellationToken cancellationToken)
    {
        if (!TryTenant(context, out var tenantId)) return Results.Unauthorized();
        if (!TryKey(context, out var key)) return MissingKey();
        if (request.InvoiceId == Guid.Empty || request.AmountMinor <= 0 || string.IsNullOrWhiteSpace(request.PaymentMethod)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = ["Invoice, positive amount, and payment method are required."] });
        var result = await handler.RecordAsync(new(tenantId, key, request.InvoiceId, request.AmountMinor, request.Date, request.PaymentMethod.Trim()), cancellationToken);
        return result.Error switch { "not-found" => Results.NotFound(), "conflict" => Conflict("Payments can only be recorded against issued, paid, or overdue invoices."), _ => Results.Created($"/api/v1/payments/{result.Value!.PaymentId}", result.Value) };
    }

    private static async Task<IResult> ReconcileAsync(Guid paymentId, ReconcilePaymentRequest request, HttpContext context, PaymentCommandHandler handler, CancellationToken cancellationToken)
    {
        if (!TryTenant(context, out var tenantId)) return Results.Unauthorized();
        if (!TryKey(context, out var key)) return MissingKey();
        if (!TryVersion(context, out var version)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["If-Match"] = ["A quoted current payment version is required."] });
        if (request.TransactionId == Guid.Empty || request.ReconciliationClaimId == Guid.Empty) return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = ["Transaction and reconciliation claim IDs are required."] });
        var result = await handler.ReconcileAsync(new(tenantId, key, paymentId, request.TransactionId, request.ReconciliationClaimId, version), cancellationToken);
        return result.Error switch { "not-found" => Results.NotFound(), "conflict" => Conflict("Payment is already reconciled or has a stale version."), "transaction-unavailable" => Unprocessable("Transaction is unavailable."), "claim-unavailable" => Unprocessable("Reconciliation claim does not match."), _ => Results.Ok(result.Value) };
    }

    private static async Task<IResult> ListAsync(Guid? invoiceId, string? search, string? sort, int? page, int? pageSize, HttpContext context, BillingDbContext database, CancellationToken cancellationToken)
    {
        if (!TryTenant(context, out var tenantId)) return Results.Unauthorized();
        if (sort is not null && sort is not ("date:asc" or "date:desc" or "amount:asc" or "amount:desc")) return Results.ValidationProblem(new Dictionary<string, string[]> { ["sort"] = ["Sort is invalid."] });
        var actualPage = Math.Max(page ?? 1, 1);
        var actualPageSize = Math.Clamp(pageSize ?? 25, 1, 100);
        var query = database.Payments.AsNoTracking().Where(item => item.TenantId == tenantId);
        if (invoiceId is not null) query = query.Where(item => item.InvoiceId == invoiceId);
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim(); query = query.Where(item => item.PaymentMethod.Contains(term)); }
        var total = await query.CountAsync(cancellationToken);
        var items = await (sort switch
        {
            "date:asc" => query.OrderBy(item => item.Date).ThenBy(item => item.Id),
            "amount:asc" => query.OrderBy(item => item.AmountMinor).ThenBy(item => item.Id),
            "amount:desc" => query.OrderByDescending(item => item.AmountMinor).ThenByDescending(item => item.Id),
            _ => query.OrderByDescending(item => item.Date).ThenByDescending(item => item.Id),
        }).Skip((actualPage - 1) * actualPageSize).Select(item => new PaymentListItem(item.Id, item.InvoiceId, item.AmountMinor, item.Date, item.PaymentMethod, item.ReconciledTransactionId, (int)item.Version)).Take(actualPageSize).ToListAsync(cancellationToken);
        return Results.Ok(new PaymentPagedResponse(items, actualPage, actualPageSize, total));
    }

    private static bool TryTenant(HttpContext context, out Guid tenantId) => Guid.TryParse(context.User.FindFirstValue("tenant_id"), out tenantId);
    private static bool TryKey(HttpContext context, out string key) { key = context.Request.Headers["Idempotency-Key"].ToString(); return Guid.TryParse(key, out _); }
    private static bool TryVersion(HttpContext context, out int version) { var value = context.Request.Headers.IfMatch.ToString(); version = 0; return value.Length >= 3 && value[0] == '"' && value[^1] == '"' && int.TryParse(value[1..^1], out version) && version > 0; }
    private static IResult MissingKey() => Results.ValidationProblem(new Dictionary<string, string[]> { ["Idempotency-Key"] = ["A GUID idempotency key is required."] });
    private static IResult Conflict(string detail) => Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflict", detail: detail);
    private static IResult Unprocessable(string detail) => Results.Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: "Validation failed", detail: detail);
}

public sealed record RecordPaymentRequest(Guid InvoiceId, long AmountMinor, DateOnly Date, string PaymentMethod);
public sealed record ReconcilePaymentRequest(Guid TransactionId, Guid ReconciliationClaimId);
public sealed record PaymentListItem(Guid PaymentId, Guid InvoiceId, long AmountMinor, DateOnly Date, string PaymentMethod, Guid? ReconciledTransactionId, int Version);
public sealed record PaymentPagedResponse(IReadOnlyList<PaymentListItem> Items, int Page, int PageSize, int TotalCount);
