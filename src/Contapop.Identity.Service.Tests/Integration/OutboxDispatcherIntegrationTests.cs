using Contapop.Identity.Service.Application.Abstractions;
using Contapop.Identity.Service.Application.Commands.ProvisionTenant;
using Contapop.Identity.Service.Infrastructure.Identity;
using Contapop.Identity.Service.Infrastructure.Outbox;
using Contapop.Identity.Service.Infrastructure.Persistence;
using Contapop.Identity.Service.Infrastructure.Persistence.Interceptors;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;

namespace Contapop.Identity.Service.Tests.Integration;

public sealed class OutboxDispatcherIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    [Fact]
    public async Task DispatchPending_publishes_each_pending_message_and_marks_it_dispatched()
    {
        var options = CreateOptions();
        await using (var database = new IdentityDbContext(options))
        {
            await database.Database.MigrateAsync();
            var handler = new ProvisionTenantCommandHandler(database, new PasswordHasher<IdentityCredential>(), new FixedClock(DateTimeOffset.UtcNow));
            await handler.HandleAsync(new ProvisionTenantCommand("Acme Studio", "Ana Garcia", "ana@acme.test", "CorrectHorseBattery1"), CancellationToken.None);
        }

        var publisher = new RecordingPublisher();
        var dispatcher = new OutboxDispatcher(new TestDbContextFactory(options), publisher, NullLogger<OutboxDispatcher>.Instance);

        await dispatcher.DispatchPendingAsync(CancellationToken.None);

        Assert.Equal(2, publisher.Messages.Count);
        await using var verification = new IdentityDbContext(options);
        Assert.All(await verification.OutboxMessages.ToListAsync(), message =>
        {
            Assert.Equal("dispatched", message.Status);
            Assert.NotNull(message.PublishedAt);
            Assert.Equal(1, message.PublishAttempts);
        });
    }

    [Fact]
    public async Task DispatchPending_retains_a_failed_message_for_retry()
    {
        var options = CreateOptions();
        await using (var database = new IdentityDbContext(options))
        {
            await database.Database.MigrateAsync();
            var handler = new ProvisionTenantCommandHandler(database, new PasswordHasher<IdentityCredential>(), new FixedClock(DateTimeOffset.UtcNow));
            await handler.HandleAsync(new ProvisionTenantCommand("Acme Studio", "Ana Garcia", "ana@acme.test", "CorrectHorseBattery1"), CancellationToken.None);
        }

        var dispatcher = new OutboxDispatcher(new TestDbContextFactory(options), new FailingPublisher(), NullLogger<OutboxDispatcher>.Instance);

        await dispatcher.DispatchPendingAsync(CancellationToken.None);

        await using var verification = new IdentityDbContext(options);
        Assert.All(await verification.OutboxMessages.ToListAsync(), message =>
        {
            Assert.Equal("pending", message.Status);
            Assert.Equal(1, message.PublishAttempts);
            Assert.NotNull(message.LastError);
        });
    }

    private DbContextOptions<IdentityDbContext> CreateOptions() => new DbContextOptionsBuilder<IdentityDbContext>()
        .UseNpgsql(_postgres.GetConnectionString())
        .AddInterceptors(new DomainEventOutboxInterceptor())
        .Options;

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    private sealed class RecordingPublisher : IIntegrationEventPublisher
    {
        public List<OutboxMessage> Messages { get; } = [];

        public Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class FailingPublisher : IIntegrationEventPublisher
    {
        public Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken) =>
            Task.FromException(new InvalidOperationException("Dapr is unavailable."));
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow => utcNow;
    }

    private sealed class TestDbContextFactory(DbContextOptions<IdentityDbContext> options) : IDbContextFactory<IdentityDbContext>
    {
        public IdentityDbContext CreateDbContext() => new(options);

        public Task<IdentityDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }
}
