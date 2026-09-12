using Contapop.Billing.Service.Infrastructure.Replication;
using Microsoft.EntityFrameworkCore;

namespace Contapop.Billing.Service.Infrastructure.Persistence;

public sealed class BillingDbContext(DbContextOptions<BillingDbContext> options) : DbContext(options)
{
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Counterparty> Counterparties => Set<Counterparty>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<ProjectReplica> ProjectReplicas => Set<ProjectReplica>();
    public DbSet<TransactionReplica> TransactionReplicas => Set<TransactionReplica>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.ToTable("invoices", "invoicing");
            entity.HasKey(invoice => invoice.Id);
            entity.Property(invoice => invoice.Id).HasColumnName("id");
            entity.Property(invoice => invoice.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(invoice => invoice.ProjectId).HasColumnName("project_id").IsRequired();
            entity.Property(invoice => invoice.CounterpartyId).HasColumnName("counterparty_id").IsRequired();
            entity.Property(invoice => invoice.Direction).HasColumnName("direction").HasMaxLength(20).IsRequired();
            entity.Property(invoice => invoice.Type).HasColumnName("type").HasMaxLength(20).IsRequired();
            entity.Property(invoice => invoice.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
            entity.Property(invoice => invoice.NetAmountMinor).HasColumnName("net_amount_minor").IsRequired();
            entity.Property(invoice => invoice.TaxAmountMinor).HasColumnName("tax_amount_minor").IsRequired();
            entity.Property(invoice => invoice.TotalAmountMinor).HasColumnName("total_amount_minor").IsRequired();
            entity.Property(invoice => invoice.Date).HasColumnName("date").IsRequired();
            entity.Property(invoice => invoice.DueDate).HasColumnName("due_date").IsRequired();
            entity.Property(invoice => invoice.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();
            entity.Property(invoice => invoice.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(invoice => invoice.UpdatedAt).HasColumnName("updated_at").IsRequired();
            entity.HasIndex(invoice => new { invoice.TenantId, invoice.ProjectId, invoice.Status });
            entity.HasIndex(invoice => new { invoice.TenantId, invoice.Status, invoice.Date });
            entity.HasMany(invoice => invoice.Lines).WithOne().HasForeignKey(line => line.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<InvoiceLine>(entity =>
        {
            entity.ToTable("invoice_lines", "invoicing");
            entity.HasKey(line => line.Id);
            entity.Property(line => line.Id).HasColumnName("id");
            entity.Property(line => line.InvoiceId).HasColumnName("invoice_id").IsRequired();
            entity.Property(line => line.Description).HasColumnName("description").HasMaxLength(500).IsRequired();
            entity.Property(line => line.Quantity).HasColumnName("quantity").IsRequired();
            entity.Property(line => line.UnitPriceMinor).HasColumnName("unit_price_minor").IsRequired();
            entity.Property(line => line.TaxRate).HasColumnName("tax_rate").HasPrecision(9, 6).IsRequired();
            entity.Property(line => line.NetAmountMinor).HasColumnName("net_amount_minor").IsRequired();
            entity.Property(line => line.TaxAmountMinor).HasColumnName("tax_amount_minor").IsRequired();
            entity.Property(line => line.TotalAmountMinor).HasColumnName("total_amount_minor").IsRequired();
            entity.HasIndex(line => line.InvoiceId);
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("payments", "payments");
            entity.HasKey(payment => payment.Id);
            entity.Property(payment => payment.Id).HasColumnName("id");
            entity.Property(payment => payment.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(payment => payment.InvoiceId).HasColumnName("invoice_id").IsRequired();
            entity.Property(payment => payment.ReconciledTransactionId).HasColumnName("reconciled_transaction_id");
            entity.Property(payment => payment.AmountMinor).HasColumnName("amount_minor").IsRequired();
            entity.Property(payment => payment.Date).HasColumnName("date").IsRequired();
            entity.Property(payment => payment.PaymentMethod).HasColumnName("payment_method").HasMaxLength(100).IsRequired();
            entity.Property(payment => payment.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();
            entity.Property(payment => payment.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(payment => payment.UpdatedAt).HasColumnName("updated_at").IsRequired();
            entity.HasIndex(payment => new { payment.TenantId, payment.InvoiceId });
        });

        modelBuilder.Entity<Counterparty>(entity =>
        {
            entity.ToTable("counterparties", "counterparties");
            entity.HasKey(counterparty => counterparty.Id);
            entity.Property(counterparty => counterparty.Id).HasColumnName("id");
            entity.Property(counterparty => counterparty.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(counterparty => counterparty.Type).HasColumnName("type").HasMaxLength(20).IsRequired();
            entity.Property(counterparty => counterparty.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            entity.Property(counterparty => counterparty.TaxId).HasColumnName("tax_id").HasMaxLength(100);
            entity.Property(counterparty => counterparty.Email).HasColumnName("email").HasMaxLength(320);
            entity.Property(counterparty => counterparty.Address).HasColumnName("address").HasMaxLength(1_000);
            entity.Property(counterparty => counterparty.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
            entity.Property(counterparty => counterparty.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();
            entity.Property(counterparty => counterparty.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(counterparty => counterparty.UpdatedAt).HasColumnName("updated_at").IsRequired();
            entity.HasIndex(counterparty => new { counterparty.TenantId, counterparty.Status, counterparty.Name });
        });

        modelBuilder.Entity<InboxMessage>(entity =>
        {
            entity.ToTable("inbox_messages", "invoicing");
            entity.HasKey(message => new { message.ConsumerName, message.EventId });
            entity.Property(message => message.ConsumerName).HasColumnName("consumer_name").HasMaxLength(200);
            entity.Property(message => message.EventId).HasColumnName("event_id");
            entity.Property(message => message.ProcessedAt).HasColumnName("processed_at").IsRequired();
        });

        modelBuilder.Entity<IdempotencyRecord>(entity =>
        {
            entity.ToTable("idempotency_records", "counterparties");
            entity.HasKey(record => new { record.TenantId, record.Operation, record.Key });
            entity.Property(record => record.TenantId).HasColumnName("tenant_id");
            entity.Property(record => record.Operation).HasColumnName("operation").HasMaxLength(100);
            entity.Property(record => record.Key).HasColumnName("key").HasMaxLength(200);
            entity.Property(record => record.Result).HasColumnName("result").HasColumnType("jsonb").IsRequired();
            entity.Property(record => record.CreatedAt).HasColumnName("created_at").IsRequired();
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("outbox_messages", "invoicing");
            entity.HasKey(message => message.EventId);
            entity.Property(message => message.EventId).HasColumnName("event_id");
            entity.Property(message => message.EventName).HasColumnName("event_name").HasMaxLength(200).IsRequired();
            entity.Property(message => message.AggregateType).HasColumnName("aggregate_type").HasMaxLength(100).IsRequired();
            entity.Property(message => message.AggregateId).HasColumnName("aggregate_id").IsRequired();
            entity.Property(message => message.AggregateVersion).HasColumnName("aggregate_version").IsRequired();
            entity.Property(message => message.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(message => message.CorrelationId).HasColumnName("correlation_id");
            entity.Property(message => message.CausationId).HasColumnName("causation_id");
            entity.Property(message => message.OccurredAt).HasColumnName("occurred_at").IsRequired();
            entity.Property(message => message.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
            entity.Property(message => message.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
            entity.HasIndex(message => new { message.Status, message.OccurredAt });
        });

        modelBuilder.Entity<ProjectReplica>(entity =>
        {
            entity.ToTable("project_replica", "invoicing");
            entity.HasKey(project => project.ProjectId);
            entity.Property(project => project.ProjectId).HasColumnName("project_id");
            entity.Property(project => project.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(project => project.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            entity.Property(project => project.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
            entity.Property(project => project.AggregateVersion).HasColumnName("aggregate_version").IsRequired();
            entity.Property(project => project.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(project => project.UpdatedAt).HasColumnName("updated_at").IsRequired();
            entity.HasIndex(project => new { project.TenantId, project.Status });
        });

        modelBuilder.Entity<TransactionReplica>(entity =>
        {
            entity.ToTable("transaction_replica", "payments");
            entity.HasKey(transaction => transaction.TransactionId);
            entity.Property(transaction => transaction.TransactionId).HasColumnName("transaction_id");
            entity.Property(transaction => transaction.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(transaction => transaction.BankAccountId).HasColumnName("bank_account_id").IsRequired();
            entity.Property(transaction => transaction.AmountMinor).HasColumnName("amount_minor").IsRequired();
            entity.Property(transaction => transaction.Date).HasColumnName("date").IsRequired();
            entity.Property(transaction => transaction.Type).HasColumnName("type").HasMaxLength(20).IsRequired();
            entity.Property(transaction => transaction.Description).HasColumnName("description").HasMaxLength(1_000);
            entity.Property(transaction => transaction.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
            entity.Property(transaction => transaction.AggregateVersion).HasColumnName("aggregate_version").IsRequired();
            entity.Property(transaction => transaction.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(transaction => transaction.UpdatedAt).HasColumnName("updated_at").IsRequired();
            entity.HasIndex(transaction => new { transaction.TenantId, transaction.Status });
        });
    }
}
