using System.Security.Claims;
using Contapop.Bookkeeping.Service.Application.Commands;
using Contapop.Bookkeeping.Service.Application.Queries;
using Contapop.Bookkeeping.Service.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Contapop.Bookkeeping.Service.Api;

public static class PlanEndpoints
{
    public static IEndpointRouteBuilder MapPlanEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var plans = endpoints.MapGroup("/api/v1/plans").RequireAuthorization("account-owner");
        plans.MapPost("", CreateAsync);
        plans.MapPatch("/{planId:guid}", UpdateAsync);
        plans.MapPost("/{planId:guid}/archive", ArchiveAsync);
        plans.MapPost("/{planId:guid}/planned-expenses", AddExpenseAsync);
        plans.MapPost("/{planId:guid}/planned-revenues", AddRevenueAsync);
        plans.MapGet("", ListAsync);
        plans.MapGet("/{planId:guid}", GetAsync);
        plans.MapGet("/{planId:guid}/plan-vs-actual", GetPlanVsActualAsync);
        return endpoints;
    }

    private static async Task<IResult> CreateAsync(CreatePlanRequest request, HttpContext context, PlanCommandHandler handler, CancellationToken ct)
    {
        if (!TryTenantAndKey(context, out var tenantId, out var key)) return MissingContext(context);
        if (!ValidPlan(request.ProjectId, request.Title, request.AllocatedAmountMinor, request.StartDate, request.EndDate)) return InvalidRequest();
        return Map(await handler.CreateAsync(new(tenantId, key, request.ProjectId, request.Title.Trim(), request.Description?.Trim(), request.AllocatedAmountMinor, request.StartDate, request.EndDate), ct), true);
    }

    private static async Task<IResult> UpdateAsync(Guid planId, UpdatePlanRequest request, HttpContext context, PlanCommandHandler handler, CancellationToken ct)
    {
        if (!TryTenantAndKey(context, out var tenantId, out var key)) return MissingContext(context);
        if (!TryVersion(context, out var version)) return MissingVersion();
        if (request.Title is not null && string.IsNullOrWhiteSpace(request.Title) || request.AllocatedAmountMinor is < 0 || request.StartDate.HasValue && request.EndDate.HasValue && request.StartDate > request.EndDate) return InvalidRequest();
        return Map(await handler.UpdateAsync(new(tenantId, key, planId, version, request.Title?.Trim(), request.Description?.Trim(), request.AllocatedAmountMinor, request.StartDate, request.EndDate), ct), false);
    }

    private static async Task<IResult> ArchiveAsync(Guid planId, HttpContext context, PlanCommandHandler handler, CancellationToken ct)
    {
        if (!TryTenantAndKey(context, out var tenantId, out var key)) return MissingContext(context);
        if (!TryVersion(context, out var version)) return MissingVersion();
        return Map(await handler.ArchiveAsync(new(tenantId, key, planId, version), ct), false);
    }

    private static Task<IResult> AddExpenseAsync(Guid planId, AddPlannedLineRequest request, HttpContext context, PlanCommandHandler handler, CancellationToken ct) => AddLineAsync(planId, request, context, command => handler.AddExpenseAsync(command, ct));
    private static Task<IResult> AddRevenueAsync(Guid planId, AddPlannedLineRequest request, HttpContext context, PlanCommandHandler handler, CancellationToken ct) => AddLineAsync(planId, request, context, command => handler.AddRevenueAsync(command, ct));

    private static async Task<IResult> AddLineAsync(Guid planId, AddPlannedLineRequest request, HttpContext context, Func<AddPlannedLineCommand, Task<PlanResult<PlannedLineResponse>>> add)
    {
        if (!TryTenantAndKey(context, out var tenantId, out var key)) return MissingContext(context);
        if (request.AmountMinor <= 0 || string.IsNullOrWhiteSpace(request.Category) || !FinancialRecord.Valid(request.Recurring, request.RecurringInterval)) return InvalidLine();
        return Map(await add(new(tenantId, key, planId, request.AmountMinor, request.Date, request.Category.Trim(), request.Recurring, request.RecurringInterval)), true);
    }

    private static async Task<IResult> ListAsync(string? status, string? search, string? sort, int? page, int? pageSize, HttpContext context, BookkeepingDbContext database, CancellationToken ct)
    {
        if (!TryTenant(context, out var tenantId)) return Results.Unauthorized();
        if (status is not null && status is not ("active" or "archived") || sort is not null && sort is not ("title:asc" or "title:desc" or "date:asc" or "date:desc")) return InvalidRequest();
        var actualPage = Math.Max(page ?? 1, 1); var actualPageSize = Math.Clamp(pageSize ?? 25, 1, 100);
        var query = database.Plans.AsNoTracking().Where(plan => plan.TenantId == tenantId);
        query = query.Where(plan => plan.Status == (status ?? "active"));
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim(); query = query.Where(plan => plan.Title.Contains(term)); }
        var total = await query.CountAsync(ct);
        var items = await (sort switch { "title:asc" => query.OrderBy(plan => plan.Title), "title:desc" => query.OrderByDescending(plan => plan.Title), "date:asc" => query.OrderBy(plan => plan.StartDate), _ => query.OrderByDescending(plan => plan.StartDate) }).ThenByDescending(plan => plan.Id).Skip((actualPage - 1) * actualPageSize).Take(actualPageSize).Select(plan => new PlanListItem(plan.Id, plan.Title, plan.AllocatedAmountMinor, plan.StartDate, plan.EndDate, plan.Status, (int)plan.Version)).ToListAsync(ct);
        return Results.Ok(new PlanPagedResponse(items, actualPage, actualPageSize, total));
    }

    private static async Task<IResult> GetAsync(Guid planId, HttpContext context, BookkeepingDbContext database, CancellationToken ct)
    {
        if (!TryTenant(context, out var tenantId)) return Results.Unauthorized();
        var plan = await database.Plans.AsNoTracking().SingleOrDefaultAsync(item => item.Id == planId && item.TenantId == tenantId, ct);
        if (plan is null) return Results.NotFound();
        var expenses = await database.PlannedExpenses.AsNoTracking().Where(item => item.PlanId == planId).OrderBy(item => item.Date).Select(item => new PlannedLineDetail(item.Id, item.AmountMinor, item.Date, item.Category, item.Recurring, item.RecurringInterval)).ToListAsync(ct);
        var revenues = await database.PlannedRevenues.AsNoTracking().Where(item => item.PlanId == planId).OrderBy(item => item.Date).Select(item => new PlannedLineDetail(item.Id, item.AmountMinor, item.Date, item.Category, item.Recurring, item.RecurringInterval)).ToListAsync(ct);
        return Results.Ok(new PlanDetailResponse(plan.Id, plan.Title, plan.Description, plan.AllocatedAmountMinor, plan.StartDate, plan.EndDate, plan.Status, expenses, revenues, plan.CreatedAt, plan.UpdatedAt, (int)plan.Version));
    }

    private static async Task<IResult> GetPlanVsActualAsync(Guid planId, HttpContext context, BookkeepingDbContext database, CancellationToken ct)
    {
        if (!TryTenant(context, out var tenantId)) return Results.Unauthorized();
        var plan = await database.Plans.AsNoTracking().SingleOrDefaultAsync(item => item.Id == planId && item.TenantId == tenantId, ct);
        if (plan is null) return Results.NotFound();
        var plannedExpenses = await database.PlannedExpenses.AsNoTracking().Where(item => item.PlanId == planId).ToListAsync(ct);
        var plannedRevenues = await database.PlannedRevenues.AsNoTracking().Where(item => item.PlanId == planId).ToListAsync(ct);
        var actualExpenses = await database.Expenses.AsNoTracking().Where(item => item.TenantId == tenantId && item.ProjectId == plan.ProjectId && item.ConfirmedAt != null && item.Date >= plan.StartDate && item.Date <= plan.EndDate).ToListAsync(ct);
        var actualRevenues = await database.Revenues.AsNoTracking().Where(item => item.TenantId == tenantId && item.ProjectId == plan.ProjectId && item.ConfirmedAt != null && item.Date >= plan.StartDate && item.Date <= plan.EndDate).ToListAsync(ct);
        var comparison = PlanVsActualCalculator.Calculate(plannedExpenses, plannedRevenues, actualExpenses, actualRevenues);
        return Results.Ok(new PlanVsActualResponse(plan.Id, new PlanPeriod(plan.StartDate, plan.EndDate), comparison.Expenses, comparison.Revenues, comparison.ByCategory));
    }

    private static IResult Map<T>(PlanResult<T> result, bool created) where T : class => result.Error switch { "not-found" => Results.NotFound(), "project-unavailable" => Unprocessable("Project is unavailable."), "outside-period" => Unprocessable("The planned line date must be within the plan period."), "conflict" => Conflict("The plan state, period, or version does not allow this operation."), _ when created => Results.Created("", result.Value), _ => Results.Ok(result.Value) };
    private static bool ValidPlan(Guid projectId, string title, long? allocatedAmountMinor, DateOnly startDate, DateOnly endDate) => projectId != Guid.Empty && !string.IsNullOrWhiteSpace(title) && allocatedAmountMinor is not < 0 && startDate <= endDate;
    private static bool TryTenant(HttpContext context, out Guid tenantId) => Guid.TryParse(context.User.FindFirstValue("tenant_id"), out tenantId);
    private static bool TryTenantAndKey(HttpContext context, out Guid tenantId, out string key) { key = context.Request.Headers["Idempotency-Key"].ToString(); return TryTenant(context, out tenantId) && Guid.TryParse(key, out _); }
    private static IResult MissingContext(HttpContext context) => context.User.Identity?.IsAuthenticated is true ? Results.ValidationProblem(new Dictionary<string, string[]> { ["Idempotency-Key"] = ["A GUID idempotency key is required."] }) : Results.Unauthorized();
    private static bool TryVersion(HttpContext context, out int version) { var value = context.Request.Headers.IfMatch.ToString(); version = 0; return value.Length >= 3 && value[0] == '"' && value[^1] == '"' && int.TryParse(value[1..^1], out version) && version > 0; }
    private static IResult MissingVersion() => Results.ValidationProblem(new Dictionary<string, string[]> { ["If-Match"] = ["A quoted current plan version is required."] });
    private static IResult InvalidRequest() => Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = ["The supplied plan fields are invalid."] });
    private static IResult InvalidLine() => Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = ["A positive amount, category, and valid recurring interval are required."] });
    private static IResult Conflict(string detail) => Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflict", detail: detail);
    private static IResult Unprocessable(string detail) => Results.Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: "Validation failed", detail: detail);
}

public sealed record CreatePlanRequest(Guid ProjectId, string Title, string? Description, long? AllocatedAmountMinor, DateOnly StartDate, DateOnly EndDate);
public sealed record UpdatePlanRequest(string? Title, string? Description, long? AllocatedAmountMinor, DateOnly? StartDate, DateOnly? EndDate);
public sealed record AddPlannedLineRequest(long AmountMinor, DateOnly Date, string Category, bool Recurring, string? RecurringInterval);
public sealed record PlanListItem(Guid PlanId, string Title, long? AllocatedAmountMinor, DateOnly StartDate, DateOnly EndDate, string Status, int Version);
public sealed record PlanPagedResponse(IReadOnlyList<PlanListItem> Items, int Page, int PageSize, int TotalCount);
public sealed record PlannedLineDetail(Guid Id, long AmountMinor, DateOnly Date, string Category, bool Recurring, string? RecurringInterval);
public sealed record PlanDetailResponse(Guid PlanId, string Title, string? Description, long? AllocatedAmountMinor, DateOnly StartDate, DateOnly EndDate, string Status, IReadOnlyList<PlannedLineDetail> PlannedExpenses, IReadOnlyList<PlannedLineDetail> PlannedRevenues, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, int Version);
public sealed record PlanPeriod(DateOnly StartDate, DateOnly EndDate);
public sealed record PlanVsActualResponse(Guid PlanId, PlanPeriod Period, PlanVsActualTotals Expenses, PlanVsActualTotals Revenues, IReadOnlyList<PlanVsActualCategory> ByCategory);
