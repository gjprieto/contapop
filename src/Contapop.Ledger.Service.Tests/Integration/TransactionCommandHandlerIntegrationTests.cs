using System.Text.Json;
using Contapop.Ledger.Service.Application.Commands;
using Contapop.Ledger.Service.Infrastructure.Persistence;
using Contapop.Ledger.Service.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Contapop.Ledger.Service.Tests.Integration;

public sealed class TransactionCommandHandlerIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    [Fact]
    public async Task Record_and_archive_write_the_required_transaction_outbox_events()
    {
        var tenantId = Guid.NewGuid();
        var bankAccountId = await SeedActiveBankAccountAsync(tenantId);
        await using var database = new LedgerDbContext(CreateOptions());
        var handler = new TransactionCommandHandler(database);

        var recorded = await handler.RecordTransactionAsync(new(tenantId, Guid.NewGuid().ToString(), bankAccountId, 1_250, new DateOnly(2026, 9, 8), "expense"), CancellationToken.None);
        var archived = await handler.ArchiveTransactionAsync(new(tenantId, Guid.NewGuid().ToString(), recorded.Value!.TransactionId, recorded.Value.Version), CancellationToken.None);

        Assert.Equal("archived", archived.Value!.Status);
        var messages = await database.OutboxMessages.OrderBy(message => message.OccurredAt).ToListAsync();
        Assert.Equal(["ledger.transaction-recorded.v1", "ledger.transaction-archived.v1"], messages.Select(message => message.EventName));
        var archivedPayload = JsonSerializer.Deserialize<JsonElement>(messages[1].Payload);
        Assert.Equal(recorded.Value.TransactionId, archivedPayload.GetProperty("transaction_id").GetGuid());
        Assert.Equal("archived", (await database.Transactions.SingleAsync()).Status);
    }

    [Fact]
    public async Task Update_writes_a_versioned_replica_synchronization_event()
    {
        var tenantId = Guid.NewGuid();
        var bankAccountId = await SeedActiveBankAccountAsync(tenantId);
        await using var database = new LedgerDbContext(CreateOptions());
        var handler = new TransactionCommandHandler(database);
        var recorded = await handler.RecordTransactionAsync(new(tenantId, Guid.NewGuid().ToString(), bankAccountId, 1_250, new DateOnly(2026, 9, 8), "expense"), CancellationToken.None);

        var updated = await handler.UpdateTransactionAsync(new(tenantId, Guid.NewGuid().ToString(), recorded.Value!.TransactionId, recorded.Value.Version, null, 2_500, null, "income"), CancellationToken.None);

        Assert.Equal(2_500, updated.Value!.AmountMinor);
        var message = await database.OutboxMessages.OrderByDescending(candidate => candidate.OccurredAt).FirstAsync();
        Assert.Equal("ledger.transaction-updated.v1", message.EventName);
        Assert.Equal(2, message.AggregateVersion);
    }

    private async Task<Guid> SeedActiveBankAccountAsync(Guid tenantId)
    {
        await using var database = new LedgerDbContext(CreateOptions());
        await database.Database.MigrateAsync();
        var account = Contapop.Ledger.Service.Domain.BankAccounts.BankAccount.Create(tenantId, Guid.NewGuid(), "ES9121000418450200051332", "CaixaBank", DateTimeOffset.UtcNow);
        database.BankAccounts.Add(account);
        await database.SaveChangesAsync();
        database.OutboxMessages.RemoveRange(database.OutboxMessages);
        await database.SaveChangesAsync();
        return account.Id;
    }

    private DbContextOptions<LedgerDbContext> CreateOptions() => new DbContextOptionsBuilder<LedgerDbContext>()
        .UseNpgsql(_postgres.GetConnectionString())
        .AddInterceptors(new DomainEventOutboxInterceptor())
        .Options;

    public Task InitializeAsync() => _postgres.StartAsync();
    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();
}
