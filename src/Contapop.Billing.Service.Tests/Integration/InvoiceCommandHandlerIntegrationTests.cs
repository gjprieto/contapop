using Contapop.Billing.Service.Application.Commands;
using Contapop.Billing.Service.Infrastructure.Persistence;
using Contapop.Billing.Service.Infrastructure.Replication;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Contapop.Billing.Service.Tests.Integration;

public sealed class InvoiceCommandHandlerIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    [Fact]
    public async Task Create_rejects_a_project_absent_from_the_local_replica()
    {
        await using var database = await CreateDatabaseAsync();
        var tenantId = Guid.NewGuid();
        var counterparty = Counterparty.Create(tenantId, "customer", "Acme SL", null, null, null, DateTimeOffset.UtcNow);
        database.Counterparties.Add(counterparty);
        await database.SaveChangesAsync();

        var result = await new InvoiceCommandHandler(database).CreateAsync(new(tenantId, Guid.NewGuid().ToString(), Guid.NewGuid(), counterparty.Id, "outgoing", 10000, 0.21m, new DateOnly(2026, 9, 9), new DateOnly(2026, 10, 9)), CancellationToken.None);

        Assert.True(result.IsProjectUnavailable);
        Assert.Equal(0, await database.Invoices.CountAsync());
    }

    [Fact]
    public async Task Issue_persists_the_invoice_and_its_outbox_event_together()
    {
        await using var database = await CreateDatabaseAsync();
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        database.ProjectReplicas.Add(ProjectReplica.Create(projectId, tenantId, "Pilot project", "active", 1, DateTimeOffset.UtcNow));
        var counterparty = Counterparty.Create(tenantId, "customer", "Acme SL", null, null, null, DateTimeOffset.UtcNow);
        database.Counterparties.Add(counterparty);
        await database.SaveChangesAsync();
        var handler = new InvoiceCommandHandler(database);

        var created = await handler.CreateAsync(new(tenantId, Guid.NewGuid().ToString(), projectId, counterparty.Id, "outgoing", 10000, 0.21m, new DateOnly(2026, 9, 9), new DateOnly(2026, 10, 9)), CancellationToken.None);
        var issued = await handler.IssueAsync(new(tenantId, Guid.NewGuid().ToString(), created.Value!.InvoiceId, created.Value.Version), CancellationToken.None);

        Assert.Equal("issued", issued.Value!.Status);
        var outbox = await database.OutboxMessages.SingleAsync();
        Assert.Equal("billing.invoice-issued.v1", outbox.EventName);
        Assert.Equal(created.Value.InvoiceId, outbox.AggregateId);
        Assert.Contains("\"total_amount_minor\":12100", outbox.Payload);
    }

    private async Task<BillingDbContext> CreateDatabaseAsync()
    {
        var database = new BillingDbContext(new DbContextOptionsBuilder<BillingDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options);
        await database.Database.MigrateAsync();
        return database;
    }

    public Task InitializeAsync() => _postgres.StartAsync();
    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();
}
