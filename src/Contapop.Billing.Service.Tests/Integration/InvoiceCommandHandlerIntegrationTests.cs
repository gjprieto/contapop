using Contapop.Billing.Service.Application.Commands;
using Contapop.Billing.Service.Application.Abstractions;
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

    [Fact]
    public async Task Record_payment_marks_a_fully_paid_invoice_and_writes_both_events()
    {
        await using var database = await CreateDatabaseAsync();
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        database.ProjectReplicas.Add(ProjectReplica.Create(projectId, tenantId, "Pilot project", "active", 1, DateTimeOffset.UtcNow));
        var counterparty = Counterparty.Create(tenantId, "customer", "Acme SL", null, null, null, DateTimeOffset.UtcNow);
        database.Counterparties.Add(counterparty);
        await database.SaveChangesAsync();
        var invoices = new InvoiceCommandHandler(database);
        var created = await invoices.CreateAsync(new(tenantId, Guid.NewGuid().ToString(), projectId, counterparty.Id, "outgoing", 10000, 0.21m, new DateOnly(2026, 9, 9), new DateOnly(2026, 10, 9)), CancellationToken.None);
        await invoices.IssueAsync(new(tenantId, Guid.NewGuid().ToString(), created.Value!.InvoiceId, created.Value.Version), CancellationToken.None);

        var recorded = await new PaymentCommandHandler(database, new AcceptClaim()).RecordAsync(new(tenantId, Guid.NewGuid().ToString(), created.Value.InvoiceId, 12100, new DateOnly(2026, 9, 10), "bank_transfer"), CancellationToken.None);

        Assert.NotNull(recorded.Value);
        Assert.Equal("paid", (await database.Invoices.SingleAsync()).Status);
        Assert.Contains(await database.OutboxMessages.Select(message => message.EventName).ToListAsync(), name => name == "billing.payment-recorded.v1");
        Assert.Contains(await database.OutboxMessages.Select(message => message.EventName).ToListAsync(), name => name == "billing.invoice-paid.v1");
    }

    [Fact]
    public async Task Reconcile_rejects_an_archived_transaction_before_validating_the_claim()
    {
        await using var database = await CreateDatabaseAsync();
        var tenantId = Guid.NewGuid();
        var invoice = Invoice.Create(tenantId, Guid.NewGuid(), Guid.NewGuid(), "outgoing", 100, 0.21m, new DateOnly(2026, 9, 9), new DateOnly(2026, 10, 9), DateTimeOffset.UtcNow);
        var payment = Payment.Create(tenantId, invoice.Id, 121, new DateOnly(2026, 9, 10), "bank_transfer", DateTimeOffset.UtcNow);
        var transactionId = Guid.NewGuid();
        database.Invoices.Add(invoice);
        database.Payments.Add(payment);
        database.TransactionReplicas.Add(TransactionReplica.Create(transactionId, tenantId, Guid.NewGuid(), 121, new DateOnly(2026, 9, 10), "income", null, "archived", 1, DateTimeOffset.UtcNow));
        await database.SaveChangesAsync();

        var result = await new PaymentCommandHandler(database, new RejectClaim()).ReconcileAsync(new(tenantId, Guid.NewGuid().ToString(), payment.Id, transactionId, Guid.NewGuid(), 1), CancellationToken.None);

        Assert.Equal("transaction-unavailable", result.Error);
    }

    [Fact]
    public async Task Reconcile_rejects_a_mismatched_claim()
    {
        await using var database = await CreateDatabaseAsync();
        var tenantId = Guid.NewGuid();
        var invoice = Invoice.Create(tenantId, Guid.NewGuid(), Guid.NewGuid(), "outgoing", 100, 0.21m, new DateOnly(2026, 9, 9), new DateOnly(2026, 10, 9), DateTimeOffset.UtcNow);
        var payment = Payment.Create(tenantId, invoice.Id, 121, new DateOnly(2026, 9, 10), "bank_transfer", DateTimeOffset.UtcNow);
        var transactionId = Guid.NewGuid();
        database.Invoices.Add(invoice);
        database.Payments.Add(payment);
        database.TransactionReplicas.Add(TransactionReplica.Create(transactionId, tenantId, Guid.NewGuid(), 121, new DateOnly(2026, 9, 10), "income", null, "active", 1, DateTimeOffset.UtcNow));
        await database.SaveChangesAsync();

        var result = await new PaymentCommandHandler(database, new RejectClaim()).ReconcileAsync(new(tenantId, Guid.NewGuid().ToString(), payment.Id, transactionId, Guid.NewGuid(), 1), CancellationToken.None);

        Assert.Equal("claim-unavailable", result.Error);
    }

    private async Task<BillingDbContext> CreateDatabaseAsync()
    {
        var database = new BillingDbContext(new DbContextOptionsBuilder<BillingDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options);
        await database.Database.MigrateAsync();
        return database;
    }

    public Task InitializeAsync() => _postgres.StartAsync();
    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    private sealed class AcceptClaim : IReconciliationClaimValidator
    {
        public Task<bool> IsValidAsync(Guid claimId, Guid transactionId, Guid paymentId, CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class RejectClaim : IReconciliationClaimValidator
    {
        public Task<bool> IsValidAsync(Guid claimId, Guid transactionId, Guid paymentId, CancellationToken cancellationToken) => Task.FromResult(false);
    }
}
