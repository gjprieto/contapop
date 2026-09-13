using Contapop.Bookkeeping.Service.Application.Commands;
using Contapop.Bookkeeping.Service.Application.Queries;
using Contapop.Bookkeeping.Service.Infrastructure.Persistence;
using Contapop.Bookkeeping.Service.Infrastructure.Replication;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Contapop.Bookkeeping.Service.Tests.Integration;

public sealed class PlanCommandIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    [Fact]
    public async Task Create_update_archive_and_add_lines_persist_with_required_outbox_events()
    {
        var tenantId = Guid.NewGuid(); var projectId = Guid.NewGuid();
        await using var database = await CreateDatabaseAsync();
        database.ProjectReplicas.Add(ProjectReplica.Create(projectId, tenantId, "Books", "active", 1, DateTimeOffset.UtcNow));
        await database.SaveChangesAsync();
        var handler = new PlanCommandHandler(database);
        var created = await handler.CreateAsync(new(tenantId, Guid.NewGuid().ToString(), projectId, "2026 plan", null, 10_000, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)), CancellationToken.None);
        var expense = await handler.AddExpenseAsync(new(tenantId, Guid.NewGuid().ToString(), created.Value!.Id, 1_000, new DateOnly(2026, 2, 1), "Software", false, null), CancellationToken.None);
        var revenue = await handler.AddRevenueAsync(new(tenantId, Guid.NewGuid().ToString(), created.Value.Id, 2_000, new DateOnly(2026, 3, 1), "Sales", false, null), CancellationToken.None);
        var updated = await handler.UpdateAsync(new(tenantId, Guid.NewGuid().ToString(), created.Value.Id, created.Value.Version, "Updated plan", "Annual", 12_000, null, null), CancellationToken.None);
        var archived = await handler.ArchiveAsync(new(tenantId, Guid.NewGuid().ToString(), created.Value.Id, updated.Value!.Version), CancellationToken.None);

        Assert.Null(expense.Error); Assert.Null(revenue.Error); Assert.Equal("archived", archived.Value!.Status);
        Assert.Equal(3, await database.OutboxMessages.CountAsync());
        Assert.Equal(new[] { "bookkeeping.plan-created.v1", "bookkeeping.planned-expense-added.v1", "bookkeeping.planned-revenue-added.v1" }, await database.OutboxMessages.OrderBy(item => item.OccurredAt).Select(item => item.EventName).ToArrayAsync());
    }

    [Fact]
    public async Task Planned_lines_and_period_updates_preserve_the_plan_period()
    {
        var tenantId = Guid.NewGuid(); var projectId = Guid.NewGuid();
        await using var database = await CreateDatabaseAsync();
        database.ProjectReplicas.Add(ProjectReplica.Create(projectId, tenantId, "Books", "active", 1, DateTimeOffset.UtcNow));
        await database.SaveChangesAsync();
        var handler = new PlanCommandHandler(database);
        var created = await handler.CreateAsync(new(tenantId, Guid.NewGuid().ToString(), projectId, "Plan", null, null, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)), CancellationToken.None);
        var outside = await handler.AddExpenseAsync(new(tenantId, Guid.NewGuid().ToString(), created.Value!.Id, 100, new DateOnly(2027, 1, 1), "Software", false, null), CancellationToken.None);
        var line = await handler.AddExpenseAsync(new(tenantId, Guid.NewGuid().ToString(), created.Value.Id, 100, new DateOnly(2026, 12, 1), "Software", false, null), CancellationToken.None);
        var invalidPeriod = await handler.UpdateAsync(new(tenantId, Guid.NewGuid().ToString(), created.Value.Id, created.Value.Version, null, null, null, null, new DateOnly(2026, 11, 30)), CancellationToken.None);

        Assert.Equal("outside-period", outside.Error); Assert.Null(line.Error); Assert.Equal("conflict", invalidPeriod.Error);
    }

    [Fact]
    public void Plan_vs_actual_keeps_expense_and_revenue_categories_distinct_and_uses_actual_minus_planned_variance()
    {
        var now = DateTimeOffset.UtcNow;
        var plan = Plan.Create(Guid.NewGuid(), Guid.NewGuid(), "Plan", null, null, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), now);
        var plannedExpense = PlannedExpense.Create(plan, 1_000, new DateOnly(2026, 2, 1), "Services", false, null, now);
        var plannedRevenue = PlannedRevenue.Create(plan, 2_000, new DateOnly(2026, 2, 1), "Services", false, null, now);
        var expense = Expense.Create(plan.TenantId, plan.ProjectId, 1_200, new DateOnly(2026, 2, 1), "Services", false, null, now);
        var revenue = Revenue.Create(plan.TenantId, plan.ProjectId, 1_500, new DateOnly(2026, 2, 1), "Services", false, null, now);

        var result = PlanVsActualCalculator.Calculate([plannedExpense], [plannedRevenue], [expense], [revenue]);

        Assert.Equal(200, result.Expenses.VarianceMinor); Assert.Equal(-500, result.Revenues.VarianceMinor);
        Assert.Contains(result.ByCategory, item => item.Type == "expense" && item.Category == "Services" && item.PlannedMinor == 1_000 && item.ActualMinor == 1_200);
        Assert.Contains(result.ByCategory, item => item.Type == "revenue" && item.Category == "Services" && item.PlannedMinor == 2_000 && item.ActualMinor == 1_500);
    }

    private async Task<BookkeepingDbContext> CreateDatabaseAsync()
    {
        var database = new BookkeepingDbContext(new DbContextOptionsBuilder<BookkeepingDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options);
        await database.Database.MigrateAsync();
        return database;
    }

    public Task InitializeAsync() => _postgres.StartAsync();
    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();
}
