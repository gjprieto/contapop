using System.Text.Json;
using Contapop.Ledger.Service.Application.ProjectReplication;
using Contapop.Ledger.Service.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Contapop.Ledger.Service.Tests.Integration;

public sealed class ProjectReplicationConsumerIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    [Fact]
    public async Task Consume_creates_a_project_replica_from_a_project_created_event()
    {
        var projectId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var occurredAt = new DateTimeOffset(2026, 9, 7, 10, 0, 0, TimeSpan.Zero);

        await ConsumeAsync(Event("identity.project-created.v1", Guid.NewGuid(), projectId, tenantId, 1, occurredAt,
            new { project_id = projectId, tenant_id = tenantId, name = "Main books", status = "active", created_at = occurredAt }));

        await using var verification = new LedgerDbContext(CreateOptions());
        var replica = await verification.ProjectReplicas.SingleAsync();
        Assert.Equal(projectId, replica.ProjectId);
        Assert.Equal(tenantId, replica.TenantId);
        Assert.Equal("Main books", replica.Name);
        Assert.Equal("active", replica.Status);
        Assert.Equal(1, replica.AggregateVersion);
    }

    [Fact]
    public async Task Consume_ignores_duplicate_event_delivery()
    {
        var projectId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var message = Event("identity.project-created.v1", eventId, projectId, tenantId, 1, DateTimeOffset.UtcNow,
            new { project_id = projectId, tenant_id = tenantId, name = "Main books", status = "active", created_at = DateTimeOffset.UtcNow });

        await ConsumeAsync(message);
        await ConsumeAsync(message);

        await using var verification = new LedgerDbContext(CreateOptions());
        Assert.Equal(1, await verification.ProjectReplicas.CountAsync());
        Assert.Equal(1, await verification.InboxMessages.CountAsync());
    }

    [Fact]
    public async Task Consume_ignores_an_out_of_order_older_aggregate_version()
    {
        var projectId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        await ConsumeAsync(Event("identity.project-created.v1", Guid.NewGuid(), projectId, tenantId, 2, DateTimeOffset.UtcNow,
            new { project_id = projectId, tenant_id = tenantId, name = "Current name", status = "active", created_at = DateTimeOffset.UtcNow }));
        await ConsumeAsync(Event("identity.project-renamed.v1", Guid.NewGuid(), projectId, tenantId, 1, DateTimeOffset.UtcNow,
            new { project_id = projectId, tenant_id = tenantId, name = "Old name", renamed_at = DateTimeOffset.UtcNow }));

        await using var verification = new LedgerDbContext(CreateOptions());
        var replica = await verification.ProjectReplicas.SingleAsync();
        Assert.Equal("Current name", replica.Name);
        Assert.Equal(2, replica.AggregateVersion);
        Assert.Equal(2, await verification.InboxMessages.CountAsync());
    }

    private async Task ConsumeAsync(IntegrationEventEnvelope envelope)
    {
        await using var database = new LedgerDbContext(CreateOptions());
        await database.Database.MigrateAsync();
        await new ProjectReplicationConsumer(database).ConsumeAsync(envelope, CancellationToken.None);
    }

    private static IntegrationEventEnvelope Event(string eventName, Guid eventId, Guid projectId, Guid tenantId, long version, DateTimeOffset occurredAt, object payload) =>
        new(eventId, eventName, "Project", projectId, version, tenantId, occurredAt, JsonSerializer.SerializeToElement(payload));

    private DbContextOptions<LedgerDbContext> CreateOptions() => new DbContextOptionsBuilder<LedgerDbContext>()
        .UseNpgsql(_postgres.GetConnectionString())
        .Options;

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();
}
