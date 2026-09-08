using Contapop.Ledger.Service.Application.Commands;
using Contapop.Ledger.Service.Domain.BankAccounts;
using Contapop.Ledger.Service.Domain.Transactions;
using Contapop.Ledger.Service.Infrastructure.Persistence;
using Contapop.Ledger.Service.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Contapop.Ledger.Service.Tests.Integration;

public sealed class ReconciliationClaimIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    [Fact]
    public async Task Reserve_is_idempotent_and_prevents_a_second_dependent_from_claiming_the_transaction()
    {
        var (tenantId, transactionId) = await SeedActiveTransactionAsync();
        await using var database = new LedgerDbContext(Options());
        var handler = new ReconciliationClaimCommandHandler(database);
        var dependentId = Guid.NewGuid();

        var first = await handler.ReserveAsync(tenantId, Guid.NewGuid().ToString(), transactionId, "expense", dependentId, CancellationToken.None);
        var replay = await handler.ReserveAsync(tenantId, Guid.NewGuid().ToString(), transactionId, "expense", dependentId, CancellationToken.None);
        var contender = await handler.ReserveAsync(tenantId, Guid.NewGuid().ToString(), transactionId, "payment", Guid.NewGuid(), CancellationToken.None);

        Assert.NotNull(first.Value);
        Assert.Equal(first.Value!.ClaimId, replay.Value!.ClaimId);
        Assert.Equal("conflict", contender.Error);
    }

    [Fact]
    public async Task Archived_transaction_cannot_be_reserved_and_confirmed_claim_can_be_explicitly_released()
    {
        var (tenantId, transactionId) = await SeedActiveTransactionAsync();
        await using var database = new LedgerDbContext(Options());
        var handler = new ReconciliationClaimCommandHandler(database);
        var reserved = await handler.ReserveAsync(tenantId, Guid.NewGuid().ToString(), transactionId, "revenue", Guid.NewGuid(), CancellationToken.None);
        var confirmed = await handler.ConfirmAsync(tenantId, Guid.NewGuid().ToString(), reserved.Value!.ClaimId, reserved.Value.Version, CancellationToken.None);
        var released = await handler.ReleaseAsync(tenantId, Guid.NewGuid().ToString(), confirmed.Value!.ClaimId, confirmed.Value.Version, CancellationToken.None);
        var transaction = await database.Transactions.SingleAsync();
        transaction.TryArchive((int)transaction.Version, DateTimeOffset.UtcNow);
        await database.SaveChangesAsync();

        var unavailable = await handler.ReserveAsync(tenantId, Guid.NewGuid().ToString(), transactionId, "expense", Guid.NewGuid(), CancellationToken.None);

        Assert.Equal("released", (await database.ReconciliationClaims.SingleAsync()).Status);
        Assert.Equal("unavailable", unavailable.Error);
        Assert.NotNull(released.Value);
    }

    private async Task<(Guid TenantId, Guid TransactionId)> SeedActiveTransactionAsync()
    {
        await using var database = new LedgerDbContext(Options());
        await database.Database.MigrateAsync();
        var tenantId = Guid.NewGuid();
        var account = BankAccount.Create(tenantId, Guid.NewGuid(), "ES9121000418450200051332", "CaixaBank", DateTimeOffset.UtcNow);
        var transaction = Transaction.Create(tenantId, account.Id, -1_000, new DateOnly(2026, 9, 8), "expense", null, DateTimeOffset.UtcNow);
        database.AddRange(account, transaction);
        await database.SaveChangesAsync();
        return (tenantId, transaction.Id);
    }

    private DbContextOptions<LedgerDbContext> Options() => new DbContextOptionsBuilder<LedgerDbContext>().UseNpgsql(_postgres.GetConnectionString()).AddInterceptors(new DomainEventOutboxInterceptor()).Options;
    public Task InitializeAsync() => _postgres.StartAsync();
    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();
}
