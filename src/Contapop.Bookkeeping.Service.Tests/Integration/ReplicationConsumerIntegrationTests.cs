using System.Text.Json;
using Contapop.Bookkeeping.Service.Application.Replication;
using Contapop.Bookkeeping.Service.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Contapop.Bookkeeping.Service.Tests.Integration;

public sealed class ReplicationConsumerIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    [Fact]
    public async Task Project_consumer_creates_replica_and_ignores_duplicate_delivery()
    {
        var projectId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var occurredAt = new DateTimeOffset(2026, 9, 12, 10, 0, 0, TimeSpan.Zero);
        var message = Event("identity.project-created.v1", eventId, projectId, tenantId, 1, occurredAt,
            new { project_id = projectId, tenant_id = tenantId, name = "Main books", status = "active", created_at = occurredAt });

        await ConsumeProjectAsync(message);
        await ConsumeProjectAsync(message);

        await using var verification = new BookkeepingDbContext(CreateOptions());
        var replica = await verification.ProjectReplicas.SingleAsync();
        Assert.Equal("Main books", replica.Name);
        Assert.Equal(1, replica.AggregateVersion);
        Assert.Equal(1, await verification.InboxMessages.CountAsync());
    }

    [Fact]
    public async Task Project_consumer_ignores_an_out_of_order_older_aggregate_version()
    {
        var projectId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        await ConsumeProjectAsync(Event("identity.project-created.v1", Guid.NewGuid(), projectId, tenantId, 2, DateTimeOffset.UtcNow,
            new { project_id = projectId, tenant_id = tenantId, name = "Current books", status = "active", created_at = DateTimeOffset.UtcNow }));
        await ConsumeProjectAsync(Event("identity.project-renamed.v1", Guid.NewGuid(), projectId, tenantId, 1, DateTimeOffset.UtcNow,
            new { project_id = projectId, tenant_id = tenantId, name = "Old books", renamed_at = DateTimeOffset.UtcNow }));

        await using var verification = new BookkeepingDbContext(CreateOptions());
        var replica = await verification.ProjectReplicas.SingleAsync();
        Assert.Equal("Current books", replica.Name);
        Assert.Equal(2, replica.AggregateVersion);
    }

    [Fact]
    public async Task Transaction_consumer_creates_replica_and_ignores_duplicate_delivery()
    {
        var transactionId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var bankAccountId = Guid.NewGuid();
        var occurredAt = new DateTimeOffset(2026, 9, 12, 10, 0, 0, TimeSpan.Zero);
        var message = Event("ledger.transaction-recorded.v1", eventId, transactionId, tenantId, 1, occurredAt,
            new { transaction_id = transactionId, tenant_id = tenantId, bank_account_id = bankAccountId, amount = 1250L, date = "2026-09-12", type = "income", description = "Invoice payment", status = "active", created_at = occurredAt });

        await ConsumeTransactionAsync(message);
        await ConsumeTransactionAsync(message);

        await using var verification = new BookkeepingDbContext(CreateOptions());
        var replica = await verification.TransactionReplicas.SingleAsync();
        Assert.Equal(bankAccountId, replica.BankAccountId);
        Assert.Equal(1250, replica.AmountMinor);
        Assert.Equal("active", replica.Status);
        Assert.Equal(1, await verification.InboxMessages.CountAsync());
    }

    [Fact]
    public async Task Transaction_consumer_ignores_an_out_of_order_older_aggregate_version()
    {
        var transactionId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var bankAccountId = Guid.NewGuid();
        await ConsumeTransactionAsync(Event("ledger.transaction-recorded.v1", Guid.NewGuid(), transactionId, tenantId, 2, DateTimeOffset.UtcNow,
            new { transaction_id = transactionId, tenant_id = tenantId, bank_account_id = bankAccountId, amount = 500L, date = "2026-09-12", type = "income", description = "Current", status = "active", created_at = DateTimeOffset.UtcNow }));
        await ConsumeTransactionAsync(Event("ledger.transaction-updated.v1", Guid.NewGuid(), transactionId, tenantId, 1, DateTimeOffset.UtcNow,
            new { transaction_id = transactionId, tenant_id = tenantId, bank_account_id = bankAccountId, amount = 100L, date = "2026-09-11", type = "expense", description = "Old", status = "active", updated_at = DateTimeOffset.UtcNow }));

        await using var verification = new BookkeepingDbContext(CreateOptions());
        var replica = await verification.TransactionReplicas.SingleAsync();
        Assert.Equal(500, replica.AmountMinor);
        Assert.Equal("income", replica.Type);
        Assert.Equal(2, replica.AggregateVersion);
    }

    private async Task ConsumeProjectAsync(IntegrationEventEnvelope envelope)
    {
        await using var database = new BookkeepingDbContext(CreateOptions());
        await database.Database.MigrateAsync();
        await new ProjectReplicationConsumer(database).ConsumeAsync(envelope, CancellationToken.None);
    }

    private async Task ConsumeTransactionAsync(IntegrationEventEnvelope envelope)
    {
        await using var database = new BookkeepingDbContext(CreateOptions());
        await database.Database.MigrateAsync();
        await new TransactionReplicationConsumer(database).ConsumeAsync(envelope, CancellationToken.None);
    }

    private static IntegrationEventEnvelope Event(string eventName, Guid eventId, Guid aggregateId, Guid tenantId, long version, DateTimeOffset occurredAt, object payload) =>
        new(eventId, eventName, "Aggregate", aggregateId, version, tenantId, occurredAt, JsonSerializer.SerializeToElement(payload));

    private DbContextOptions<BookkeepingDbContext> CreateOptions() => new DbContextOptionsBuilder<BookkeepingDbContext>()
        .UseNpgsql(_postgres.GetConnectionString())
        .Options;

    public Task InitializeAsync() => _postgres.StartAsync();
    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();
}
