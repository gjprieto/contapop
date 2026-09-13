using Contapop.Bookkeeping.Service.Application.Abstractions;
using Contapop.Bookkeeping.Service.Application.Commands;
using Contapop.Bookkeeping.Service.Infrastructure.Persistence;
using Contapop.Bookkeeping.Service.Infrastructure.Replication;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Contapop.Bookkeeping.Service.Tests.Integration;

public sealed class DocumentImportIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    [Fact]
    public async Task Imported_draft_is_excluded_until_confirmation_then_publishes_recorded_event()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        await using var database = await CreateDatabaseAsync();
        database.ProjectReplicas.Add(ProjectReplica.Create(projectId, tenantId, "Books", "active", 1, DateTimeOffset.UtcNow));
        await database.SaveChangesAsync();
        var handler = new FinancialRecordCommandHandler(database, new ClaimValidator(), new ExtractionAdapter(new(1_250, new DateOnly(2026, 9, 12), "Software", 0.9m, null)));

        var imported = await handler.ImportExpenseAsync(new(tenantId, Guid.NewGuid().ToString(), projectId, "%PDF-1.4 sample receipt"u8.ToArray()), CancellationToken.None);
        var reportFacingBeforeConfirmation = await database.Expenses.CountAsync(item => item.TenantId == tenantId && item.ConfirmedAt != null);
        var confirmed = await handler.ConfirmExpenseAsync(new(tenantId, Guid.NewGuid().ToString(), imported.Value!.Id, imported.Value.Version, null, null, null, null, null, false), CancellationToken.None);
        var reportFacingAfterConfirmation = await database.Expenses.CountAsync(item => item.TenantId == tenantId && item.ConfirmedAt != null);

        Assert.Null(imported.Error);
        Assert.Equal("pdf_ocr", imported.Value.ImportSource);
        Assert.Null(imported.Value.ConfirmedAt);
        Assert.Equal(0, reportFacingBeforeConfirmation);
        Assert.Null(confirmed.Error);
        Assert.NotNull(confirmed.Value!.ConfirmedAt);
        Assert.Equal(1, reportFacingAfterConfirmation);
        var outbox = await database.OutboxMessages.SingleAsync();
        Assert.Equal("bookkeeping.expense-recorded.v1", outbox.EventName);
    }

    [Fact]
    public async Task Missing_extracted_amount_creates_a_reviewable_draft()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        await using var database = await CreateDatabaseAsync();
        database.ProjectReplicas.Add(ProjectReplica.Create(projectId, tenantId, "Books", "active", 1, DateTimeOffset.UtcNow));
        await database.SaveChangesAsync();
        var handler = new FinancialRecordCommandHandler(database, new ClaimValidator(), new ExtractionAdapter(new(null, null, null, null, "No totals found.")));

        var imported = await handler.ImportRevenueAsync(new(tenantId, Guid.NewGuid().ToString(), projectId, "%PDF-1.4 sample receipt"u8.ToArray()), CancellationToken.None);

        Assert.Null(imported.Error);
        Assert.Null(imported.Value!.AmountMinor);
        Assert.Null(imported.Value.ConfirmedAt);
        Assert.Empty(await database.OutboxMessages.ToListAsync());
    }

    private async Task<BookkeepingDbContext> CreateDatabaseAsync()
    {
        var database = new BookkeepingDbContext(new DbContextOptionsBuilder<BookkeepingDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options);
        await database.Database.MigrateAsync();
        return database;
    }

    public Task InitializeAsync() => _postgres.StartAsync();
    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    private sealed class ClaimValidator : IReconciliationClaimValidator
    {
        public Task<bool> IsValidAsync(Guid claimId, Guid transactionId, string dependentType, Guid dependentId, CancellationToken cancellationToken) => Task.FromResult(false);
    }

    private sealed class ExtractionAdapter(DocumentExtractionResult result) : IDocumentExtractionAdapter
    {
        public Task<DocumentExtractionResult> ExtractAsync(ReadOnlyMemory<byte> document, CancellationToken cancellationToken) => Task.FromResult(result);
    }
}
