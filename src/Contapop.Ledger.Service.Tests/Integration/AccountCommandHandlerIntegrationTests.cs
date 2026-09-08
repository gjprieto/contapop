using System.Text.Json;
using Contapop.Ledger.Service.Application.Commands;
using Contapop.Ledger.Service.Infrastructure.Persistence;
using Contapop.Ledger.Service.Infrastructure.Replication;
using Contapop.Ledger.Service.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Contapop.Ledger.Service.Tests.Integration;

public sealed class AccountCommandHandlerIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    [Fact]
    public async Task LinkBankAccount_creates_an_account_and_outbox_event_without_account_number()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        await SeedProjectAsync(tenantId, projectId, "active");

        await using (var database = new LedgerDbContext(CreateOptions()))
        {
            var result = await new AccountCommandHandler(database).LinkBankAccountAsync(
                new(tenantId, Guid.NewGuid().ToString(), projectId, "ES91 2100 0418 4502 0005 1332", "CaixaBank"), CancellationToken.None);

            Assert.NotNull(result.Value);
            Assert.Equal("active", result.Value.Status);
            Assert.Equal(1, result.Value.Version);
        }

        await using var verification = new LedgerDbContext(CreateOptions());
        var account = await verification.BankAccounts.SingleAsync();
        Assert.Equal("ES91 2100 0418 4502 0005 1332", account.AccountNumber);
        var message = await verification.OutboxMessages.SingleAsync();
        Assert.Equal("ledger.bank-account-linked.v1", message.EventName);
        var payload = JsonSerializer.Deserialize<JsonElement>(message.Payload);
        Assert.Equal(account.Id, payload.GetProperty("bank_account_id").GetGuid());
        Assert.False(payload.TryGetProperty("account_number", out _));
    }

    [Fact]
    public async Task LinkBankAccount_rejects_a_project_missing_from_the_local_replica()
    {
        await using var database = new LedgerDbContext(CreateOptions());
        await database.Database.MigrateAsync();

        var result = await new AccountCommandHandler(database).LinkBankAccountAsync(
            new(Guid.NewGuid(), Guid.NewGuid().ToString(), Guid.NewGuid(), "ES9121000418450200051332", "CaixaBank"), CancellationToken.None);

        Assert.True(result.IsProjectUnavailable);
        Assert.Empty(await database.BankAccounts.ToListAsync());
        Assert.Empty(await database.OutboxMessages.ToListAsync());
    }

    [Fact]
    public async Task AddPaymentCardLabel_creates_a_card_and_outbox_event()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        await SeedProjectAsync(tenantId, projectId, "active");

        await using (var database = new LedgerDbContext(CreateOptions()))
        {
            var result = await new AccountCommandHandler(database).AddPaymentCardLabelAsync(
                new(tenantId, Guid.NewGuid().ToString(), projectId, "Visa ending 1234", "Ana Garcia", new DateOnly(2028, 12, 1)), CancellationToken.None);

            Assert.NotNull(result.Value);
            Assert.Equal("Visa ending 1234", result.Value.Label);
        }

        await using var verification = new LedgerDbContext(CreateOptions());
        var message = await verification.OutboxMessages.SingleAsync();
        Assert.Equal("ledger.card-linked.v1", message.EventName);
        var payload = JsonSerializer.Deserialize<JsonElement>(message.Payload);
        Assert.Equal("Visa ending 1234", payload.GetProperty("label").GetString());
    }

    [Fact]
    public async Task LinkBankAccount_replays_the_original_result_for_the_same_idempotency_key()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var idempotencyKey = Guid.NewGuid().ToString();
        await SeedProjectAsync(tenantId, projectId, "active");
        await using var database = new LedgerDbContext(CreateOptions());
        var handler = new AccountCommandHandler(database);
        var command = new LinkBankAccountCommand(tenantId, idempotencyKey, projectId, "ES9121000418450200051332", "CaixaBank");

        var first = await handler.LinkBankAccountAsync(command, CancellationToken.None);
        var replay = await handler.LinkBankAccountAsync(command, CancellationToken.None);

        Assert.Equal(first.Value, replay.Value);
        Assert.Equal(1, await database.BankAccounts.CountAsync());
        Assert.Equal(1, await database.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task ArchiveAndRemove_apply_the_expected_version()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        await SeedProjectAsync(tenantId, projectId, "active");
        await using var database = new LedgerDbContext(CreateOptions());
        var handler = new AccountCommandHandler(database);
        var account = (await handler.LinkBankAccountAsync(new(tenantId, Guid.NewGuid().ToString(), projectId, "ES9121000418450200051332", "CaixaBank"), CancellationToken.None)).Value!;
        var card = (await handler.AddPaymentCardLabelAsync(new(tenantId, Guid.NewGuid().ToString(), projectId, "Visa ending 1234", "Ana Garcia", null), CancellationToken.None)).Value!;

        var staleArchive = await handler.ArchiveBankAccountAsync(new(tenantId, Guid.NewGuid().ToString(), account.BankAccountId, 2), CancellationToken.None);
        var archive = await handler.ArchiveBankAccountAsync(new(tenantId, Guid.NewGuid().ToString(), account.BankAccountId, 1), CancellationToken.None);
        var staleRemoval = await handler.RemovePaymentCardLabelAsync(new(tenantId, Guid.NewGuid().ToString(), card.CardId, 2), CancellationToken.None);
        var removal = await handler.RemovePaymentCardLabelAsync(new(tenantId, Guid.NewGuid().ToString(), card.CardId, 1), CancellationToken.None);

        Assert.True(staleArchive.IsConflict);
        Assert.Equal("archived", archive.Value!.Status);
        Assert.Equal(2, archive.Value.Version);
        Assert.True(staleRemoval.IsConflict);
        Assert.True(removal.Succeeded);
    }

    private async Task SeedProjectAsync(Guid tenantId, Guid projectId, string status)
    {
        await using var database = new LedgerDbContext(CreateOptions());
        await database.Database.MigrateAsync();
        database.ProjectReplicas.Add(ProjectReplica.Create(projectId, tenantId, "Main books", status, 1, DateTimeOffset.UtcNow));
        await database.SaveChangesAsync();
    }

    private DbContextOptions<LedgerDbContext> CreateOptions() => new DbContextOptionsBuilder<LedgerDbContext>()
        .UseNpgsql(_postgres.GetConnectionString())
        .AddInterceptors(new DomainEventOutboxInterceptor())
        .Options;

    public Task InitializeAsync() => _postgres.StartAsync();
    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();
}
