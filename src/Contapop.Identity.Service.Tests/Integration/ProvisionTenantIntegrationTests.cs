using System.Text.Json;
using Contapop.Identity.Service.Application.Abstractions;
using Contapop.Identity.Service.Application.Commands.ProvisionTenant;
using Contapop.Identity.Service.Infrastructure.Identity;
using Contapop.Identity.Service.Infrastructure.Persistence;
using Contapop.Identity.Service.Infrastructure.Persistence.Interceptors;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Contapop.Identity.Service.Tests.Integration;

public sealed class ProvisionTenantIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    [Fact]
    public async Task Handle_creates_tenant_owner_project_credential_and_outbox_events_in_one_transaction()
    {
        var options = CreateOptions();
        await using (var setup = new IdentityDbContext(options))
        {
            await setup.Database.MigrateAsync();
        }

        var createdAt = new DateTimeOffset(2026, 9, 6, 14, 30, 0, TimeSpan.Zero);
        ProvisionTenantResult result;
        await using (var database = new IdentityDbContext(options))
        {
            var handler = new ProvisionTenantCommandHandler(
                database,
                new PasswordHasher<IdentityCredential>(),
                new FixedClock(createdAt));

            result = (await handler.HandleAsync(
                new ProvisionTenantCommand("Acme Studio", "Ana Garcia", "ana@acme.test", "CorrectHorseBattery1"),
                CancellationToken.None))!;
        }

        await using var verification = new IdentityDbContext(options);
        Assert.Equal("Acme Studio", (await verification.Tenants.SingleAsync()).Name);
        Assert.Equal(result.OwnerUserId, (await verification.DomainUsers.SingleAsync()).Id);
        Assert.Equal(result.ProjectId, (await verification.Projects.SingleAsync()).Id);

        var credential = await verification.Set<IdentityCredential>().SingleAsync();
        Assert.Equal(result.OwnerUserId, credential.DomainUserId);
        Assert.Equal(PasswordVerificationResult.Success,
            new PasswordHasher<IdentityCredential>().VerifyHashedPassword(credential, credential.PasswordHash!, "CorrectHorseBattery1"));

        var messages = await verification.OutboxMessages.OrderBy(message => message.EventName).ToListAsync();
        Assert.Equal(["identity.project-created.v1", "identity.tenant-created.v1"], messages.Select(message => message.EventName));
        Assert.All(messages, message => Assert.Equal("pending", message.Status));

        using var tenantPayload = JsonDocument.Parse(messages[1].Payload);
        Assert.Equal(result.TenantId, tenantPayload.RootElement.GetProperty("tenant_id").GetGuid());
        Assert.Equal(result.OwnerUserId, tenantPayload.RootElement.GetProperty("owner_user_id").GetGuid());

        using var projectPayload = JsonDocument.Parse(messages[0].Payload);
        Assert.Equal(result.ProjectId, projectPayload.RootElement.GetProperty("project_id").GetGuid());
        Assert.Equal("active", projectPayload.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Handle_rejects_an_owner_email_that_is_already_provisioned()
    {
        var options = CreateOptions();
        await using (var setup = new IdentityDbContext(options))
        {
            await setup.Database.MigrateAsync();
        }

        await using var database = new IdentityDbContext(options);
        var handler = new ProvisionTenantCommandHandler(
            database,
            new PasswordHasher<IdentityCredential>(),
            new FixedClock(DateTimeOffset.UtcNow));
        var command = new ProvisionTenantCommand("Acme Studio", "Ana Garcia", "ana@acme.test", "CorrectHorseBattery1");

        Assert.NotNull(await handler.HandleAsync(command, CancellationToken.None));
        Assert.Null(await handler.HandleAsync(command, CancellationToken.None));
        Assert.Equal(1, await database.DomainUsers.CountAsync());
        Assert.Equal(2, await database.OutboxMessages.CountAsync());
    }

    private DbContextOptions<IdentityDbContext> CreateOptions() => new DbContextOptionsBuilder<IdentityDbContext>()
        .UseNpgsql(_postgres.GetConnectionString())
        .AddInterceptors(new DomainEventOutboxInterceptor())
        .Options;

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow => utcNow;
    }
}
