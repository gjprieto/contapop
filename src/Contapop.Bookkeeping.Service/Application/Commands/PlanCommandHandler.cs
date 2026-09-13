using System.Text.Json;
using Contapop.Bookkeeping.Service.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Contapop.Bookkeeping.Service.Application.Commands;

public sealed class PlanCommandHandler(BookkeepingDbContext database)
{
    public async Task<PlanResult<PlanResponse>> CreateAsync(CreatePlanCommand command, CancellationToken ct)
    {
        var replay = await ReplayAsync<PlanResponse>(command.TenantId, "create-plan", command.IdempotencyKey, ct);
        if (replay is not null) return PlanResult<PlanResponse>.Success(replay);
        if (!await ProjectIsActive(command.TenantId, command.ProjectId, ct)) return PlanResult<PlanResponse>.ProjectUnavailable();
        var now = DateTimeOffset.UtcNow;
        var plan = Plan.Create(command.TenantId, command.ProjectId, command.Title, command.Description, command.AllocatedAmountMinor, command.StartDate, command.EndDate, now);
        var response = ToResponse(plan);
        database.Plans.Add(plan);
        AddOutbox("bookkeeping.plan-created.v1", "Plan", plan.Id, plan.Version, plan.TenantId, now, new Dictionary<string, object?> { ["plan_id"] = plan.Id, ["project_id"] = plan.ProjectId, ["title"] = plan.Title, ["allocated_amount_minor"] = plan.AllocatedAmountMinor, ["start_date"] = plan.StartDate, ["end_date"] = plan.EndDate, ["created_at"] = now });
        AddIdempotency(command.TenantId, "create-plan", command.IdempotencyKey, response, now);
        await database.SaveChangesAsync(ct);
        return PlanResult<PlanResponse>.Success(response);
    }

    public async Task<PlanResult<PlanResponse>> UpdateAsync(UpdatePlanCommand command, CancellationToken ct)
    {
        var replay = await ReplayAsync<PlanResponse>(command.TenantId, "update-plan", command.IdempotencyKey, ct);
        if (replay is not null) return PlanResult<PlanResponse>.Success(replay);
        var plan = await database.Plans.SingleOrDefaultAsync(item => item.Id == command.PlanId && item.TenantId == command.TenantId, ct);
        if (plan is null) return PlanResult<PlanResponse>.NotFound();
        var start = command.StartDate ?? plan.StartDate; var end = command.EndDate ?? plan.EndDate;
        var hasOutOfPeriodLine = await database.PlannedExpenses.AnyAsync(item => item.PlanId == plan.Id && (item.Date < start || item.Date > end), ct) || await database.PlannedRevenues.AnyAsync(item => item.PlanId == plan.Id && (item.Date < start || item.Date > end), ct);
        if (!plan.TryUpdate(command.Title, command.Description, command.AllocatedAmountMinor, command.StartDate, command.EndDate, command.ExpectedVersion, hasOutOfPeriodLine, DateTimeOffset.UtcNow)) return PlanResult<PlanResponse>.Conflict();
        var response = ToResponse(plan); AddIdempotency(command.TenantId, "update-plan", command.IdempotencyKey, response, plan.UpdatedAt);
        await database.SaveChangesAsync(ct);
        return PlanResult<PlanResponse>.Success(response);
    }

    public async Task<PlanResult<PlanResponse>> ArchiveAsync(ArchivePlanCommand command, CancellationToken ct)
    {
        var replay = await ReplayAsync<PlanResponse>(command.TenantId, "archive-plan", command.IdempotencyKey, ct);
        if (replay is not null) return PlanResult<PlanResponse>.Success(replay);
        var plan = await database.Plans.SingleOrDefaultAsync(item => item.Id == command.PlanId && item.TenantId == command.TenantId, ct);
        if (plan is null) return PlanResult<PlanResponse>.NotFound();
        if (!plan.TryArchive(command.ExpectedVersion, DateTimeOffset.UtcNow)) return PlanResult<PlanResponse>.Conflict();
        var response = ToResponse(plan); AddIdempotency(command.TenantId, "archive-plan", command.IdempotencyKey, response, plan.UpdatedAt);
        await database.SaveChangesAsync(ct);
        return PlanResult<PlanResponse>.Success(response);
    }

    public Task<PlanResult<PlannedLineResponse>> AddExpenseAsync(AddPlannedLineCommand command, CancellationToken ct) => AddLineAsync<PlannedExpense>(command, "add-planned-expense", "bookkeeping.planned-expense-added.v1", "PlannedExpense", "planned_expense_id", ct);
    public Task<PlanResult<PlannedLineResponse>> AddRevenueAsync(AddPlannedLineCommand command, CancellationToken ct) => AddLineAsync<PlannedRevenue>(command, "add-planned-revenue", "bookkeeping.planned-revenue-added.v1", "PlannedRevenue", "planned_revenue_id", ct);

    private async Task<PlanResult<PlannedLineResponse>> AddLineAsync<T>(AddPlannedLineCommand command, string operation, string eventName, string aggregateType, string idName, CancellationToken ct) where T : PlannedLine
    {
        var replay = await ReplayAsync<PlannedLineResponse>(command.TenantId, operation, command.IdempotencyKey, ct);
        if (replay is not null) return PlanResult<PlannedLineResponse>.Success(replay);
        var plan = await database.Plans.SingleOrDefaultAsync(item => item.Id == command.PlanId && item.TenantId == command.TenantId, ct);
        if (plan is null) return PlanResult<PlannedLineResponse>.NotFound();
        if (plan.Status != "active") return PlanResult<PlannedLineResponse>.Conflict();
        if (command.Date < plan.StartDate || command.Date > plan.EndDate) return PlanResult<PlannedLineResponse>.OutsidePeriod();
        var now = DateTimeOffset.UtcNow;
        PlannedLine line = typeof(T) == typeof(PlannedExpense) ? PlannedExpense.Create(plan, command.AmountMinor, command.Date, command.Category, command.Recurring, command.RecurringInterval, now) : PlannedRevenue.Create(plan, command.AmountMinor, command.Date, command.Category, command.Recurring, command.RecurringInterval, now);
        database.Add(line); var response = ToResponse(line);
        AddOutbox(eventName, aggregateType, line.Id, 1, line.TenantId, now, new Dictionary<string, object?> { [idName] = line.Id, ["plan_id"] = line.PlanId, ["project_id"] = line.ProjectId, ["amount_minor"] = line.AmountMinor, ["date"] = line.Date, ["category"] = line.Category, ["recurring"] = line.Recurring, ["recurring_interval"] = line.RecurringInterval, ["created_at"] = now });
        AddIdempotency(command.TenantId, operation, command.IdempotencyKey, response, now);
        await database.SaveChangesAsync(ct);
        return PlanResult<PlannedLineResponse>.Success(response);
    }

    private Task<bool> ProjectIsActive(Guid tenantId, Guid projectId, CancellationToken ct) => database.ProjectReplicas.AnyAsync(project => project.ProjectId == projectId && project.TenantId == tenantId && project.Status == "active", ct);
    private void AddOutbox(string eventName, string aggregateType, Guid aggregateId, long version, Guid tenantId, DateTimeOffset at, object payload) => database.OutboxMessages.Add(OutboxMessage.Create(eventName, aggregateType, aggregateId, version, tenantId, at, JsonSerializer.Serialize(payload)));
    private void AddIdempotency<T>(Guid tenantId, string operation, string key, T response, DateTimeOffset at) => database.IdempotencyRecords.Add(IdempotencyRecord.Create(tenantId, operation, key, JsonSerializer.Serialize(response), at));
    private async Task<T?> ReplayAsync<T>(Guid tenantId, string operation, string key, CancellationToken ct) where T : class => JsonSerializer.Deserialize<T>(await database.IdempotencyRecords.AsNoTracking().Where(item => item.TenantId == tenantId && item.Operation == operation && item.Key == key).Select(item => item.Result).SingleOrDefaultAsync(ct) ?? "null");
    public static PlanResponse ToResponse(Plan plan) => new(plan.Id, plan.ProjectId, plan.Title, plan.Description, plan.AllocatedAmountMinor, plan.StartDate, plan.EndDate, plan.Status, plan.CreatedAt, plan.UpdatedAt, (int)plan.Version);
    public static PlannedLineResponse ToResponse(PlannedLine line) => new(line.Id, line.PlanId, line.AmountMinor, line.Date, line.Category, line.Recurring, line.RecurringInterval, line.CreatedAt);
}

public sealed record CreatePlanCommand(Guid TenantId, string IdempotencyKey, Guid ProjectId, string Title, string? Description, long? AllocatedAmountMinor, DateOnly StartDate, DateOnly EndDate);
public sealed record UpdatePlanCommand(Guid TenantId, string IdempotencyKey, Guid PlanId, int ExpectedVersion, string? Title, string? Description, long? AllocatedAmountMinor, DateOnly? StartDate, DateOnly? EndDate);
public sealed record ArchivePlanCommand(Guid TenantId, string IdempotencyKey, Guid PlanId, int ExpectedVersion);
public sealed record AddPlannedLineCommand(Guid TenantId, string IdempotencyKey, Guid PlanId, long AmountMinor, DateOnly Date, string Category, bool Recurring, string? RecurringInterval);
public sealed record PlanResponse(Guid Id, Guid ProjectId, string Title, string? Description, long? AllocatedAmountMinor, DateOnly StartDate, DateOnly EndDate, string Status, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, int Version);
public sealed record PlannedLineResponse(Guid Id, Guid PlanId, long AmountMinor, DateOnly Date, string Category, bool Recurring, string? RecurringInterval, DateTimeOffset CreatedAt);
public sealed class PlanResult<T> where T : class { public T? Value { get; private init; } public string? Error { get; private init; } public static PlanResult<T> Success(T value) => new() { Value = value }; public static PlanResult<T> NotFound() => new() { Error = "not-found" }; public static PlanResult<T> Conflict() => new() { Error = "conflict" }; public static PlanResult<T> ProjectUnavailable() => new() { Error = "project-unavailable" }; public static PlanResult<T> OutsidePeriod() => new() { Error = "outside-period" }; }
