using Contapop.Billing.Service.Application.Commands;
using Contapop.Billing.Service.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Contapop.Billing.Service.Tests.Integration;

public sealed class CounterpartyCommandHandlerIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    [Fact]
    public async Task Create_update_and_archive_persist_a_counterparty_for_its_tenant()
    {
        var tenantId = Guid.NewGuid();
        await using var database = new BillingDbContext(CreateOptions());
        await database.Database.MigrateAsync();
        var handler = new CounterpartyCommandHandler(database);

        var created = await handler.CreateAsync(new(tenantId, Guid.NewGuid().ToString(), "customer", " Acme SL ", "ESA123", "billing@acme.test", null), CancellationToken.None);
        var updated = await handler.UpdateAsync(new(tenantId, Guid.NewGuid().ToString(), created.Value!.CounterpartyId, created.Value.Version, "Acme Spain SL", null, null, "Madrid"), CancellationToken.None);
        var archived = await handler.ArchiveAsync(new(tenantId, Guid.NewGuid().ToString(), created.Value.CounterpartyId, updated.Value!.Version), CancellationToken.None);

        Assert.Equal("Acme Spain SL", updated.Value.Name);
        Assert.Equal("archived", archived.Value!.Status);
        var persisted = await database.Counterparties.SingleAsync();
        Assert.Equal("Madrid", persisted.Address);
        Assert.Equal("archived", persisted.Status);
        Assert.Equal(3, persisted.Version);
    }

    [Fact]
    public async Task Create_replays_the_original_result_for_the_same_idempotency_key()
    {
        var tenantId = Guid.NewGuid();
        var key = Guid.NewGuid().ToString();
        await using var database = new BillingDbContext(CreateOptions());
        await database.Database.MigrateAsync();
        var handler = new CounterpartyCommandHandler(database);

        var first = await handler.CreateAsync(new(tenantId, key, "supplier", "Paper Co", null, null, null), CancellationToken.None);
        var replay = await handler.CreateAsync(new(tenantId, key, "supplier", "Different", null, null, null), CancellationToken.None);

        Assert.Equal(first.Value!.CounterpartyId, replay.Value!.CounterpartyId);
        Assert.Equal(1, await database.Counterparties.CountAsync());
    }

    private DbContextOptions<BillingDbContext> CreateOptions() => new DbContextOptionsBuilder<BillingDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options;
    public Task InitializeAsync() => _postgres.StartAsync();
    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();
}
