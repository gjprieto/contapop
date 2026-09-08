using Contapop.Ledger.Service.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Contapop.Ledger.Service.Tests.Integration;

public sealed class LedgerSchemaMigrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    [Fact]
    public async Task Migrate_creates_ledger_schemas_and_replication_tables()
    {
        await using var database = new LedgerDbContext(CreateOptions());
        await database.Database.MigrateAsync();

        var tables = await database.Database.SqlQuery<string>($"""
            SELECT table_schema || '.' || table_name
            FROM information_schema.tables
            WHERE table_schema IN ('bank_accounts', 'payment_cards', 'transactions')
            ORDER BY table_schema, table_name
            """).ToListAsync();

        Assert.Contains("bank_accounts.bank_accounts", tables);
        Assert.Contains("bank_accounts.inbox_messages", tables);
        Assert.Contains("bank_accounts.idempotency_records", tables);
        Assert.Contains("bank_accounts.outbox_messages", tables);
        Assert.Contains("bank_accounts.project_replica", tables);
        Assert.Contains("payment_cards.payment_cards", tables);
        Assert.Contains("transactions.transactions", tables);
        Assert.Contains("transactions.reconciliation_claims", tables);

        var replicaColumns = await GetColumnNamesAsync(database, "bank_accounts", "project_replica");
        Assert.Equal(
            ["aggregate_version", "created_at", "name", "project_id", "status", "tenant_id", "updated_at"],
            replicaColumns);

        var accountColumns = await GetColumnNamesAsync(database, "bank_accounts", "bank_accounts");
        Assert.Contains("version", accountColumns);

        var cardColumns = await GetColumnNamesAsync(database, "payment_cards", "payment_cards");
        Assert.Contains("version", cardColumns);
    }

    private static Task<List<string>> GetColumnNamesAsync(LedgerDbContext database, string schema, string table) =>
        database.Database.SqlQuery<string>($"""
            SELECT column_name
            FROM information_schema.columns
            WHERE table_schema = {schema} AND table_name = {table}
            ORDER BY column_name
            """).ToListAsync();

    private DbContextOptions<LedgerDbContext> CreateOptions() => new DbContextOptionsBuilder<LedgerDbContext>()
        .UseNpgsql(_postgres.GetConnectionString())
        .Options;

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();
}
