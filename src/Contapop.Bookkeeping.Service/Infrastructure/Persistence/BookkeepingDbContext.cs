using Contapop.Bookkeeping.Service.Infrastructure.Replication;
using Microsoft.EntityFrameworkCore;

namespace Contapop.Bookkeeping.Service.Infrastructure.Persistence;

public sealed class BookkeepingDbContext(DbContextOptions<BookkeepingDbContext> options) : DbContext(options)
{
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
    public DbSet<ProjectReplica> ProjectReplicas => Set<ProjectReplica>();
    public DbSet<TransactionReplica> TransactionReplicas => Set<TransactionReplica>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<Revenue> Revenues => Set<Revenue>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<PlannedExpense> PlannedExpenses => Set<PlannedExpense>();
    public DbSet<PlannedRevenue> PlannedRevenues => Set<PlannedRevenue>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InboxMessage>(entity =>
        {
            entity.ToTable("inbox_messages", "expenses");
            entity.HasKey(message => new { message.ConsumerName, message.EventId });
            entity.Property(message => message.ConsumerName).HasColumnName("consumer_name").HasMaxLength(200);
            entity.Property(message => message.EventId).HasColumnName("event_id");
            entity.Property(message => message.ProcessedAt).HasColumnName("processed_at").IsRequired();
        });

        modelBuilder.Entity<ProjectReplica>(entity =>
        {
            entity.ToTable("project_replica", "expenses");
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
            entity.ToTable("transaction_replica", "expenses");
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

        ConfigureFinancialRecord(modelBuilder.Entity<Expense>(), "expenses", "expenses");
        ConfigureFinancialRecord(modelBuilder.Entity<Revenue>(), "revenues", "revenues");
        modelBuilder.Entity<Plan>(entity =>
        {
            entity.ToTable("plans", "planning");
            entity.HasKey(plan => plan.Id);
            entity.Property(plan => plan.Id).HasColumnName("id");
            entity.Property(plan => plan.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(plan => plan.ProjectId).HasColumnName("project_id").IsRequired();
            entity.Property(plan => plan.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
            entity.Property(plan => plan.Description).HasColumnName("description").HasMaxLength(2_000);
            entity.Property(plan => plan.AllocatedAmountMinor).HasColumnName("allocated_amount_minor");
            entity.Property(plan => plan.StartDate).HasColumnName("start_date").IsRequired();
            entity.Property(plan => plan.EndDate).HasColumnName("end_date").IsRequired();
            entity.Property(plan => plan.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
            entity.Property(plan => plan.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();
            entity.Property(plan => plan.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(plan => plan.UpdatedAt).HasColumnName("updated_at").IsRequired();
            entity.HasIndex(plan => new { plan.TenantId, plan.Status });
        });
        ConfigurePlannedLine(modelBuilder.Entity<PlannedExpense>(), "planned_expenses");
        ConfigurePlannedLine(modelBuilder.Entity<PlannedRevenue>(), "planned_revenues");

        modelBuilder.Entity<IdempotencyRecord>(entity =>
        {
            entity.ToTable("idempotency_records", "expenses");
            entity.HasKey(record => new { record.TenantId, record.Operation, record.Key });
            entity.Property(record => record.TenantId).HasColumnName("tenant_id");
            entity.Property(record => record.Operation).HasColumnName("operation").HasMaxLength(100);
            entity.Property(record => record.Key).HasColumnName("key").HasMaxLength(200);
            entity.Property(record => record.Result).HasColumnName("result").HasColumnType("jsonb").IsRequired();
            entity.Property(record => record.CreatedAt).HasColumnName("created_at").IsRequired();
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("outbox_messages", "expenses");
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
    }

    private static void ConfigureFinancialRecord<T>(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<T> entity, string table, string schema) where T : FinancialRecord
    {
        entity.ToTable(table, schema);
        entity.HasKey(record => record.Id);
        entity.Property(record => record.Id).HasColumnName("id");
        entity.Property(record => record.TenantId).HasColumnName("tenant_id").IsRequired();
        entity.Property(record => record.ProjectId).HasColumnName("project_id").IsRequired();
        entity.Property(record => record.ReconciledTransactionId).HasColumnName("reconciled_transaction_id");
        entity.Property(record => record.AmountMinor).HasColumnName("amount_minor").IsRequired();
        entity.Property(record => record.Date).HasColumnName("date").IsRequired();
        entity.Property(record => record.Category).HasColumnName("category").HasMaxLength(200).IsRequired();
        entity.Property(record => record.Recurring).HasColumnName("recurring").IsRequired();
        entity.Property(record => record.RecurringInterval).HasColumnName("recurring_interval").HasMaxLength(20);
        entity.Property(record => record.ImportSource).HasColumnName("import_source").HasMaxLength(20).IsRequired();
        entity.Property(record => record.ConfirmedAt).HasColumnName("confirmed_at");
        entity.Property(record => record.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();
        entity.Property(record => record.CreatedAt).HasColumnName("created_at").IsRequired();
        entity.Property(record => record.UpdatedAt).HasColumnName("updated_at").IsRequired();
        entity.HasIndex(record => new { record.TenantId, record.ProjectId, record.Date });
        entity.HasIndex(record => new { record.TenantId, record.ReconciledTransactionId }).IsUnique().HasFilter("reconciled_transaction_id IS NOT NULL");
    }

    private static void ConfigurePlannedLine<T>(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<T> entity, string table) where T : PlannedLine
    {
        entity.ToTable(table, "planning");
        entity.HasKey(line => line.Id);
        entity.Property(line => line.Id).HasColumnName("id");
        entity.Property(line => line.TenantId).HasColumnName("tenant_id").IsRequired();
        entity.Property(line => line.ProjectId).HasColumnName("project_id").IsRequired();
        entity.Property(line => line.PlanId).HasColumnName("plan_id").IsRequired();
        entity.Property(line => line.AmountMinor).HasColumnName("amount_minor").IsRequired();
        entity.Property(line => line.Date).HasColumnName("date").IsRequired();
        entity.Property(line => line.Category).HasColumnName("category").HasMaxLength(200).IsRequired();
        entity.Property(line => line.Recurring).HasColumnName("recurring").IsRequired();
        entity.Property(line => line.RecurringInterval).HasColumnName("recurring_interval").HasMaxLength(20);
        entity.Property(line => line.CreatedAt).HasColumnName("created_at").IsRequired();
        entity.Property(line => line.UpdatedAt).HasColumnName("updated_at").IsRequired();
        entity.HasIndex(line => new { line.TenantId, line.PlanId, line.Date });
    }
}
