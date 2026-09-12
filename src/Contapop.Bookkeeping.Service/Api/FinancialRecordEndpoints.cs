using System.Security.Claims;
using Contapop.Bookkeeping.Service.Application.Commands;
using Contapop.Bookkeeping.Service.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Contapop.Bookkeeping.Service.Api;

public static class FinancialRecordEndpoints
{
    public static IEndpointRouteBuilder MapFinancialRecordEndpoints(this IEndpointRouteBuilder endpoints)
    {
        Map(endpoints, "/api/v1/expenses", "expense", static database => database.Expenses, static (handler, command, ct) => handler.CreateExpenseAsync(command, ct), static (handler, command, ct) => handler.UpdateExpenseAsync(command, ct), static (handler, command, ct) => handler.DeleteExpenseAsync(command, ct), static (handler, command, ct) => handler.ReconcileExpenseAsync(command, ct));
        Map(endpoints, "/api/v1/revenues", "revenue", static database => database.Revenues, static (handler, command, ct) => handler.CreateRevenueAsync(command, ct), static (handler, command, ct) => handler.UpdateRevenueAsync(command, ct), static (handler, command, ct) => handler.DeleteRevenueAsync(command, ct), static (handler, command, ct) => handler.ReconcileRevenueAsync(command, ct));
        return endpoints;
    }

    private static void Map<T>(IEndpointRouteBuilder endpoints, string route, string name, Func<BookkeepingDbContext, DbSet<T>> records, Func<FinancialRecordCommandHandler, CreateFinancialRecordCommand, CancellationToken, Task<RecordResult<FinancialRecordResponse>>> create, Func<FinancialRecordCommandHandler, UpdateFinancialRecordCommand, CancellationToken, Task<RecordResult<FinancialRecordResponse>>> update, Func<FinancialRecordCommandHandler, DeleteFinancialRecordCommand, CancellationToken, Task<RecordResult<DeletedFinancialRecordResponse>>> delete, Func<FinancialRecordCommandHandler, ReconcileFinancialRecordCommand, CancellationToken, Task<RecordResult<FinancialRecordResponse>>> reconcile) where T : FinancialRecord
    {
        var group = endpoints.MapGroup(route).RequireAuthorization("account-owner");
        group.MapPost("", async (CreateFinancialRecordRequest request, HttpContext context, FinancialRecordCommandHandler handler, CancellationToken ct) =>
        {
            if (!TryTenantAndKey(context, out var tenantId, out var key)) return MissingContext(context);
            if (!Valid(request.ProjectId, request.AmountMinor, request.Category, request.Recurring, request.RecurringInterval)) return InvalidRequest();
            return Map(await create(handler, new(tenantId, key, request.ProjectId, request.AmountMinor, request.Date, request.Category.Trim(), request.Recurring, request.RecurringInterval), ct), true, name);
        });
        group.MapPatch("/{recordId:guid}", async (Guid recordId, UpdateFinancialRecordRequest request, HttpContext context, FinancialRecordCommandHandler handler, CancellationToken ct) =>
        {
            if (!TryTenantAndKey(context, out var tenantId, out var key)) return MissingContext(context);
            if (!TryVersion(context, out var version)) return MissingVersion();
            if (request.AmountMinor is <= 0 || request.Category is not null && string.IsNullOrWhiteSpace(request.Category) || request.Recurring is false && request.RecurringInterval is not null || request.RecurringInterval is not null && request.RecurringInterval is not ("weekly" or "monthly" or "yearly")) return InvalidRequest();
            return Map(await update(handler, new(tenantId, key, recordId, version, request.AmountMinor, request.Date, request.Category?.Trim(), request.Recurring, request.RecurringInterval, request.RecurringInterval is not null || request.Recurring is false), ct), false, name);
        });
        group.MapDelete("/{recordId:guid}", async (Guid recordId, HttpContext context, FinancialRecordCommandHandler handler, CancellationToken ct) =>
        {
            if (!TryTenantAndKey(context, out var tenantId, out var key)) return MissingContext(context);
            if (!TryVersion(context, out var version)) return MissingVersion();
            var result = await delete(handler, new(tenantId, key, recordId, version), ct);
            return result.Error switch { "not-found" => Results.NotFound(), "conflict" => Conflict("Record has a stale version."), _ => Results.NoContent() };
        });
        group.MapPost("/{recordId:guid}/reconcile", async (Guid recordId, ReconcileFinancialRecordRequest request, HttpContext context, FinancialRecordCommandHandler handler, CancellationToken ct) =>
        {
            if (!TryTenantAndKey(context, out var tenantId, out var key)) return MissingContext(context);
            if (!TryVersion(context, out var version)) return MissingVersion();
            if (request.TransactionId == Guid.Empty || request.ReconciliationClaimId == Guid.Empty) return InvalidRequest();
            return Map(await reconcile(handler, new(tenantId, key, recordId, request.TransactionId, request.ReconciliationClaimId, version), ct), false, name);
        });
        group.MapGet("", async (string? category, bool? includeDrafts, DateOnly? dateFrom, DateOnly? dateTo, string? search, string? sort, int? page, int? pageSize, HttpContext context, BookkeepingDbContext database, CancellationToken ct) =>
        {
            if (!Guid.TryParse(context.User.FindFirstValue("tenant_id"), out var tenantId)) return Results.Unauthorized();
            if (sort is not null && sort is not ("date:asc" or "date:desc" or "amount:asc" or "amount:desc")) return Results.ValidationProblem(new Dictionary<string, string[]> { ["sort"] = ["Sort is invalid."] });
            var actualPage = Math.Max(page ?? 1, 1); var actualPageSize = Math.Clamp(pageSize ?? 25, 1, 100);
            var query = records(database).AsNoTracking().Where(item => item.TenantId == tenantId);
            if (includeDrafts != true) query = query.Where(item => item.ConfirmedAt != null);
            if (!string.IsNullOrWhiteSpace(category)) query = query.Where(item => item.Category == category.Trim());
            if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim(); query = query.Where(item => item.Category.Contains(term)); }
            if (dateFrom.HasValue) query = query.Where(item => item.Date >= dateFrom.Value);
            if (dateTo.HasValue) query = query.Where(item => item.Date <= dateTo.Value);
            var total = await query.CountAsync(ct);
            var items = await (sort switch { "date:asc" => query.OrderBy(item => item.Date), "amount:asc" => query.OrderBy(item => item.AmountMinor), "amount:desc" => query.OrderByDescending(item => item.AmountMinor), _ => query.OrderByDescending(item => item.Date) }).ThenByDescending(item => item.Id).Skip((actualPage - 1) * actualPageSize).Select(item => new FinancialRecordListItem(item.Id, item.AmountMinor, item.Date, item.Category, item.Recurring, item.RecurringInterval, item.ImportSource, item.ConfirmedAt, item.ReconciledTransactionId, (int)item.Version)).Take(actualPageSize).ToListAsync(ct);
            return Results.Ok(new FinancialRecordPagedResponse(items, actualPage, actualPageSize, total));
        });
    }

    private static IResult Map(RecordResult<FinancialRecordResponse> result, bool created, string name) => result.Error switch { "not-found" => Results.NotFound(), "conflict" => Conflict("Record is already reconciled or has a stale version."), "project-unavailable" => Unprocessable("Project is unavailable."), "transaction-unavailable" => Unprocessable("Transaction is unavailable."), "claim-unavailable" => Unprocessable("Reconciliation claim does not match."), _ when created => Results.Created($"/api/v1/{name}s/{result.Value!.Id}", result.Value), _ => Results.Ok(result.Value) };
    private static bool Valid(Guid projectId, long amountMinor, string category, bool recurring, string? interval) => projectId != Guid.Empty && amountMinor > 0 && !string.IsNullOrWhiteSpace(category) && FinancialRecord.Valid(recurring, interval);
    private static bool TryTenantAndKey(HttpContext context, out Guid tenantId, out string key) { tenantId = Guid.Empty; key = context.Request.Headers["Idempotency-Key"].ToString(); return Guid.TryParse(context.User.FindFirstValue("tenant_id"), out tenantId) && Guid.TryParse(key, out _); }
    private static IResult MissingContext(HttpContext context) => context.User.Identity?.IsAuthenticated is true ? Results.ValidationProblem(new Dictionary<string, string[]> { ["Idempotency-Key"] = ["A GUID idempotency key is required."] }) : Results.Unauthorized();
    private static bool TryVersion(HttpContext context, out int version) { var value = context.Request.Headers.IfMatch.ToString(); version = 0; return value.Length >= 3 && value[0] == '"' && value[^1] == '"' && int.TryParse(value[1..^1], out version) && version > 0; }
    private static IResult MissingVersion() => Results.ValidationProblem(new Dictionary<string, string[]> { ["If-Match"] = ["A quoted current record version is required."] });
    private static IResult InvalidRequest() => Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = ["Project, positive amount, category, and a valid recurring interval are required."] });
    private static IResult Conflict(string detail) => Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflict", detail: detail);
    private static IResult Unprocessable(string detail) => Results.Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: "Validation failed", detail: detail);
}

public sealed record CreateFinancialRecordRequest(Guid ProjectId, long AmountMinor, DateOnly Date, string Category, bool Recurring, string? RecurringInterval);
public sealed record UpdateFinancialRecordRequest(long? AmountMinor, DateOnly? Date, string? Category, bool? Recurring, string? RecurringInterval);
public sealed record ReconcileFinancialRecordRequest(Guid TransactionId, Guid ReconciliationClaimId);
public sealed record FinancialRecordListItem(Guid Id, long AmountMinor, DateOnly Date, string Category, bool Recurring, string? RecurringInterval, string ImportSource, DateTimeOffset? ConfirmedAt, Guid? ReconciledTransactionId, int Version);
public sealed record FinancialRecordPagedResponse(IReadOnlyList<FinancialRecordListItem> Items, int Page, int PageSize, int TotalCount);
