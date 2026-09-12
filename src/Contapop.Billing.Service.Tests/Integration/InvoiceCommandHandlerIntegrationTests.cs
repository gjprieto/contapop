using Contapop.Billing.Service.Application.Commands;
using Contapop.Billing.Service.Application.BackgroundJobs;
using Contapop.Billing.Service.Application.Abstractions;
using Contapop.Billing.Service.Infrastructure.Persistence;
using Contapop.Billing.Service.Infrastructure.Replication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
    public async Task Invoice_list_query_sorts_mapped_fields_before_projecting_its_response()
    {
        await using var database = await CreateDatabaseAsync();
        var tenantId = Guid.NewGuid();
        var counterparty = Counterparty.Create(tenantId, "customer", "Acme SL", null, null, null, DateTimeOffset.UtcNow);
        var invoice = Invoice.Create(tenantId, Guid.NewGuid(), counterparty.Id, "outgoing", "service", [new CreateInvoiceLine("Consulting", 1, 10_000, 0.21m)], new DateOnly(2026, 9, 9), new DateOnly(2026, 10, 9), DateTimeOffset.UtcNow);
        database.AddRange(counterparty, invoice);
        await database.SaveChangesAsync();

        var items = await (from listedInvoice in database.Invoices.AsNoTracking()
                           join listedCounterparty in database.Counterparties.AsNoTracking() on listedInvoice.CounterpartyId equals listedCounterparty.Id
                           where listedInvoice.TenantId == tenantId
                           select new { Invoice = listedInvoice, CounterpartyName = listedCounterparty.Name })
            .OrderByDescending(item => item.Invoice.Date).ThenByDescending(item => item.Invoice.Id)
            .Select(item => new { item.Invoice.Id, item.CounterpartyName, item.Invoice.Type, item.Invoice.Version })
            .ToListAsync();

        Assert.Collection(items, item =>
        {
            Assert.Equal(invoice.Id, item.Id);
            Assert.Equal("Acme SL", item.CounterpartyName);
            Assert.Equal("service", item.Type);
            Assert.Equal(1, item.Version);
        });
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
    public async Task Archive_soft_deletes_an_unpaid_invoice_and_writes_the_required_event()
    {
        await using var database = await CreateDatabaseAsync();
        var tenantId = Guid.NewGuid();
        var counterparty = Counterparty.Create(tenantId, "customer", "Acme SL", null, null, null, DateTimeOffset.UtcNow);
        var invoice = Invoice.Create(tenantId, Guid.NewGuid(), counterparty.Id, "outgoing", 100, 0.21m, new DateOnly(2026, 9, 9), new DateOnly(2026, 10, 9), DateTimeOffset.UtcNow);
        database.AddRange(counterparty, invoice);
        await database.SaveChangesAsync();

        var archived = await new InvoiceCommandHandler(database).ArchiveAsync(new(tenantId, Guid.NewGuid().ToString(), invoice.Id, 1), CancellationToken.None);

        Assert.Equal("archived", archived.Value!.Status);
        Assert.Equal("archived", await database.Invoices.Where(item => item.Id == invoice.Id).Select(item => item.Status).SingleAsync());
        Assert.Equal(1, await database.InvoiceLines.CountAsync(line => line.InvoiceId == invoice.Id));
        var @event = await database.OutboxMessages.SingleAsync(message => message.EventName == "billing.invoice-archived.v1");
        Assert.Equal(invoice.Id, @event.AggregateId);
        Assert.Contains("\"previous_status\":\"draft\"", @event.Payload);
        Assert.Contains("\"project_id\"", @event.Payload);
    }

    [Fact]
    public async Task Archive_rejects_an_invoice_with_a_payment_and_replays_an_archived_invoice()
    {
        await using var database = await CreateDatabaseAsync();
        var tenantId = Guid.NewGuid();
        var counterparty = Counterparty.Create(tenantId, "customer", "Acme SL", null, null, null, DateTimeOffset.UtcNow);
        var paidReference = Invoice.Create(tenantId, Guid.NewGuid(), counterparty.Id, "outgoing", 100, 0.21m, new DateOnly(2026, 9, 9), new DateOnly(2026, 10, 9), DateTimeOffset.UtcNow);
        var archiveable = Invoice.Create(tenantId, Guid.NewGuid(), counterparty.Id, "outgoing", 100, 0.21m, new DateOnly(2026, 9, 9), new DateOnly(2026, 10, 9), DateTimeOffset.UtcNow);
        database.AddRange(counterparty, paidReference, archiveable, Payment.Create(tenantId, paidReference.Id, 121, new DateOnly(2026, 9, 10), "bank_transfer", DateTimeOffset.UtcNow));
        await database.SaveChangesAsync();
        var handler = new InvoiceCommandHandler(database);

        var rejected = await handler.ArchiveAsync(new(tenantId, Guid.NewGuid().ToString(), paidReference.Id, 1), CancellationToken.None);
        var archived = await handler.ArchiveAsync(new(tenantId, Guid.NewGuid().ToString(), archiveable.Id, 1), CancellationToken.None);
        var replay = await handler.ArchiveAsync(new(tenantId, Guid.NewGuid().ToString(), archiveable.Id, 1), CancellationToken.None);

        Assert.True(rejected.IsConflict);
        Assert.Equal("archived", archived.Value!.Status);
        Assert.Equal(archived.Value, replay.Value);
        Assert.Equal(1, await database.OutboxMessages.CountAsync(message => message.EventName == "billing.invoice-archived.v1"));
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
    public async Task Payment_list_query_returns_the_current_payment_version_for_reconciliation()
    {
        await using var database = await CreateDatabaseAsync();
        var tenantId = Guid.NewGuid();
        var payment = Payment.Create(tenantId, Guid.NewGuid(), 12_100, new DateOnly(2026, 9, 10), "bank_transfer", DateTimeOffset.UtcNow);
        database.Payments.Add(payment);
        await database.SaveChangesAsync();

        var listed = await database.Payments.AsNoTracking()
            .Where(item => item.TenantId == tenantId)
            .Select(item => new { item.Id, item.Version })
            .SingleAsync();

        Assert.Equal(payment.Id, listed.Id);
        Assert.Equal(1, listed.Version);
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

    [Fact]
    public async Task Mark_overdue_marks_only_past_due_issued_invoices_and_writes_an_outbox_event()
    {
        await using var database = await CreateDatabaseAsync();
        var now = new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);
        var overdueInvoice = Invoice.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "outgoing", 100, 0.21m, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 9), now);
        var currentInvoice = Invoice.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "outgoing", 100, 0.21m, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10), now);
        Assert.True(overdueInvoice.TryIssue(1, now));
        Assert.True(currentInvoice.TryIssue(1, now));
        database.Invoices.AddRange(overdueInvoice, currentInvoice);
        await database.SaveChangesAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<BillingDbContext>(options => options.UseNpgsql(_postgres.GetConnectionString()));
        await using var provider = services.BuildServiceProvider();
        var job = new MarkInvoicesOverdueJob(provider.GetRequiredService<IServiceScopeFactory>(), new FixedTimeProvider(now), provider.GetRequiredService<ILogger<MarkInvoicesOverdueJob>>());

        Assert.Equal(1, await job.RunAsync(CancellationToken.None));

        await using var verification = new BillingDbContext(CreateOptions());
        Assert.Equal("overdue", await verification.Invoices.Where(invoice => invoice.Id == overdueInvoice.Id).Select(invoice => invoice.Status).SingleAsync());
        Assert.Equal("issued", await verification.Invoices.Where(invoice => invoice.Id == currentInvoice.Id).Select(invoice => invoice.Status).SingleAsync());
        var @event = await verification.OutboxMessages.SingleAsync(message => message.EventName == "billing.invoice-overdue.v1");
        Assert.Equal(overdueInvoice.Id, @event.AggregateId);
        Assert.Contains("\"due_date\": \"2026-09-09\"", @event.Payload);
    }

    private async Task<BillingDbContext> CreateDatabaseAsync()
    {
        var database = new BillingDbContext(CreateOptions());
        await database.Database.MigrateAsync();
        return database;
    }

    private DbContextOptions<BillingDbContext> CreateOptions() => new DbContextOptionsBuilder<BillingDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options;

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

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
