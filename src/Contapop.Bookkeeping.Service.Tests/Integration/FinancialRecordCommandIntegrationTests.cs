using Contapop.Bookkeeping.Service.Application.Abstractions;
using Contapop.Bookkeeping.Service.Application.Commands;
using Contapop.Bookkeeping.Service.Infrastructure.Persistence;
using Contapop.Bookkeeping.Service.Infrastructure.Replication;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Contapop.Bookkeeping.Service.Tests.Integration;

public sealed class FinancialRecordCommandIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    [Fact]
    public async Task Create_update_and_delete_expense_persist_atomically_and_write_the_outbox()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        await using var database = await CreateDatabaseAsync();
        database.ProjectReplicas.Add(ProjectReplica.Create(projectId, tenantId, "Books", "active", 1, DateTimeOffset.UtcNow));
        await database.SaveChangesAsync();
        var handler = new FinancialRecordCommandHandler(database, new ClaimValidator(), new ExtractionAdapter());

        var created = await handler.CreateExpenseAsync(new(tenantId, Guid.NewGuid().ToString(), projectId, 1_250, new DateOnly(2026, 9, 12), "Software", false, null), CancellationToken.None);
        var updated = await handler.UpdateExpenseAsync(new(tenantId, Guid.NewGuid().ToString(), created.Value!.Id, created.Value.Version, 1_500, null, "Tools", true, "monthly", true), CancellationToken.None);
        var deleted = await handler.DeleteExpenseAsync(new(tenantId, Guid.NewGuid().ToString(), created.Value.Id, updated.Value!.Version), CancellationToken.None);

        Assert.Null(created.Error);
        Assert.Equal("manual", created.Value.ImportSource);
        Assert.NotNull(created.Value.ConfirmedAt);
        Assert.Equal("Tools", updated.Value.Category);
        Assert.True(updated.Value.Recurring);
        Assert.Null(deleted.Error);
        Assert.Empty(await database.Expenses.ToListAsync());
        var outbox = await database.OutboxMessages.SingleAsync();
        Assert.Equal("bookkeeping.expense-recorded.v1", outbox.EventName);
    }

    [Fact]
    public async Task Reconciliation_rejects_unavailable_transaction_and_mismatched_claim()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        await using var database = await CreateDatabaseAsync();
        database.ProjectReplicas.Add(ProjectReplica.Create(projectId, tenantId, "Books", "active", 1, DateTimeOffset.UtcNow));
        await database.SaveChangesAsync();
        var handler = new FinancialRecordCommandHandler(database, new ClaimValidator(), new ExtractionAdapter());
        var created = await handler.CreateRevenueAsync(new(tenantId, Guid.NewGuid().ToString(), projectId, 2_000, new DateOnly(2026, 9, 12), "Sales", false, null), CancellationToken.None);

        var unavailable = await handler.ReconcileRevenueAsync(new(tenantId, Guid.NewGuid().ToString(), created.Value!.Id, transactionId, Guid.NewGuid(), created.Value.Version), CancellationToken.None);
        database.TransactionReplicas.Add(TransactionReplica.Create(transactionId, tenantId, Guid.NewGuid(), 2_000, new DateOnly(2026, 9, 12), "income", null, "active", 1, DateTimeOffset.UtcNow));
        await database.SaveChangesAsync();
        var mismatched = await handler.ReconcileRevenueAsync(new(tenantId, Guid.NewGuid().ToString(), created.Value.Id, transactionId, Guid.NewGuid(), created.Value.Version), CancellationToken.None);

        Assert.Equal("transaction-unavailable", unavailable.Error);
        Assert.Equal("claim-unavailable", mismatched.Error);
    }

    [Fact]
    public async Task Reconciliation_rejects_an_archived_transaction_replica()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        await using var database = await CreateDatabaseAsync();
        database.ProjectReplicas.Add(ProjectReplica.Create(projectId, tenantId, "Books", "active", 1, DateTimeOffset.UtcNow));
        database.TransactionReplicas.Add(TransactionReplica.Create(transactionId, tenantId, Guid.NewGuid(), 750, new DateOnly(2026, 9, 12), "expense", null, "archived", 1, DateTimeOffset.UtcNow));
        await database.SaveChangesAsync();
        var handler = new FinancialRecordCommandHandler(database, new ClaimValidator(), new ExtractionAdapter());
        var created = await handler.CreateExpenseAsync(new(tenantId, Guid.NewGuid().ToString(), projectId, 750, new DateOnly(2026, 9, 12), "Travel", false, null), CancellationToken.None);

        var result = await handler.ReconcileExpenseAsync(new(tenantId, Guid.NewGuid().ToString(), created.Value!.Id, transactionId, Guid.NewGuid(), created.Value.Version), CancellationToken.None);

        Assert.Equal("transaction-unavailable", result.Error);
    }

    [Fact]
    public async Task Reconciliation_persists_when_the_ledger_claim_matches()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        await using var database = await CreateDatabaseAsync();
        database.ProjectReplicas.Add(ProjectReplica.Create(projectId, tenantId, "Books", "active", 1, DateTimeOffset.UtcNow));
        database.TransactionReplicas.Add(TransactionReplica.Create(transactionId, tenantId, Guid.NewGuid(), 1_100, new DateOnly(2026, 9, 12), "expense", null, "active", 1, DateTimeOffset.UtcNow));
        await database.SaveChangesAsync();
        var handler = new FinancialRecordCommandHandler(database, new ClaimValidator(true), new ExtractionAdapter());
        var created = await handler.CreateExpenseAsync(new(tenantId, Guid.NewGuid().ToString(), projectId, 1_100, new DateOnly(2026, 9, 12), "Office", false, null), CancellationToken.None);

        var result = await handler.ReconcileExpenseAsync(new(tenantId, Guid.NewGuid().ToString(), created.Value!.Id, transactionId, Guid.NewGuid(), created.Value.Version), CancellationToken.None);

        Assert.Null(result.Error);
        Assert.Equal(transactionId, result.Value!.ReconciledTransactionId);
    }

    private async Task<BookkeepingDbContext> CreateDatabaseAsync()
    {
        var database = new BookkeepingDbContext(new DbContextOptionsBuilder<BookkeepingDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options);
        await database.Database.MigrateAsync();
        return database;
    }

    public Task InitializeAsync() => _postgres.StartAsync();
    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    private sealed class ClaimValidator(bool result = false) : IReconciliationClaimValidator
    {
        public Task<bool> IsValidAsync(Guid claimId, Guid transactionId, string dependentType, Guid dependentId, CancellationToken cancellationToken) => Task.FromResult(result);
    }

    private sealed class ExtractionAdapter : IDocumentExtractionAdapter
    {
        public Task<DocumentExtractionResult> ExtractAsync(ReadOnlyMemory<byte> document, CancellationToken cancellationToken) => Task.FromResult(new DocumentExtractionResult(1_250, new DateOnly(2026, 9, 12), "Software", 0.9m, null));
    }
}
