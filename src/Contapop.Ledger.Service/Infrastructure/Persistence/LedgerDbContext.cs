using Contapop.Ledger.Service.Domain.BankAccounts;
using Contapop.Ledger.Service.Domain.PaymentCards;
using Contapop.Ledger.Service.Domain.Transactions;
using Contapop.Ledger.Service.Domain.Reconciliation;
using Contapop.Ledger.Service.Infrastructure.Messaging;
using Contapop.Ledger.Service.Infrastructure.Outbox;
using Contapop.Ledger.Service.Infrastructure.Replication;
using Microsoft.EntityFrameworkCore;

namespace Contapop.Ledger.Service.Infrastructure.Persistence;

public sealed class LedgerDbContext(DbContextOptions<LedgerDbContext> options) : DbContext(options)
{
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
    public DbSet<PaymentCard> PaymentCards => Set<PaymentCard>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<TransactionReconciliationClaim> ReconciliationClaims => Set<TransactionReconciliationClaim>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
    public DbSet<ProjectReplica> ProjectReplicas => Set<ProjectReplica>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BankAccount>(entity =>
        {
            entity.ToTable("bank_accounts", "bank_accounts");
            entity.HasKey(account => account.Id);
            entity.Property(account => account.Id).HasColumnName("id");
            entity.Property(account => account.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(account => account.ProjectId).HasColumnName("project_id").IsRequired();
            entity.Property(account => account.AccountNumber).HasColumnName("account_number").HasMaxLength(200).IsRequired();
            entity.Property(account => account.BankName).HasColumnName("bank_name").HasMaxLength(200).IsRequired();
            entity.Property(account => account.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
            entity.Property(account => account.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();
            entity.Property(account => account.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(account => account.UpdatedAt).HasColumnName("updated_at").IsRequired();
            entity.HasIndex(account => new { account.TenantId, account.ProjectId });
        });

        modelBuilder.Entity<PaymentCard>(entity =>
        {
            entity.ToTable("payment_cards", "payment_cards");
            entity.HasKey(card => card.Id);
            entity.Property(card => card.Id).HasColumnName("id");
            entity.Property(card => card.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(card => card.ProjectId).HasColumnName("project_id").IsRequired();
            entity.Property(card => card.Label).HasColumnName("label").HasMaxLength(200).IsRequired();
            entity.Property(card => card.CardholderName).HasColumnName("cardholder_name").HasMaxLength(200).IsRequired();
            entity.Property(card => card.ExpirationDate).HasColumnName("expiration_date");
            entity.Property(card => card.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();
            entity.Property(card => card.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(card => card.UpdatedAt).HasColumnName("updated_at").IsRequired();
            entity.HasIndex(card => new { card.TenantId, card.ProjectId });
        });

        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.ToTable("transactions", "transactions");
            entity.HasKey(transaction => transaction.Id);
            entity.Property(transaction => transaction.Id).HasColumnName("id");
            entity.Property(transaction => transaction.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(transaction => transaction.BankAccountId).HasColumnName("bank_account_id").IsRequired();
            entity.Property(transaction => transaction.AmountMinor).HasColumnName("amount_minor").IsRequired();
            entity.Property(transaction => transaction.Date).HasColumnName("date").IsRequired();
            entity.Property(transaction => transaction.Type).HasColumnName("type").HasMaxLength(20).IsRequired();
            entity.Property(transaction => transaction.Description).HasColumnName("description").HasMaxLength(1_000);
            entity.Property(transaction => transaction.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
            entity.Property(transaction => transaction.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();
            entity.Property(transaction => transaction.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(transaction => transaction.UpdatedAt).HasColumnName("updated_at").IsRequired();
            entity.HasIndex(transaction => new { transaction.TenantId, transaction.BankAccountId, transaction.Date });
        });

        modelBuilder.Entity<TransactionReconciliationClaim>(entity =>
        {
            entity.ToTable("reconciliation_claims", "transactions");
            entity.HasKey(claim => claim.Id);
            entity.Property(claim => claim.Id).HasColumnName("id");
            entity.Property(claim => claim.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(claim => claim.TransactionId).HasColumnName("transaction_id").IsRequired();
            entity.Property(claim => claim.DependentType).HasColumnName("dependent_type").HasMaxLength(20).IsRequired();
            entity.Property(claim => claim.DependentId).HasColumnName("dependent_id").IsRequired();
            entity.Property(claim => claim.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
            entity.Property(claim => claim.ExpiresAt).HasColumnName("expires_at").IsRequired();
            entity.Property(claim => claim.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(claim => claim.ConfirmedAt).HasColumnName("confirmed_at");
            entity.Property(claim => claim.ReleasedAt).HasColumnName("released_at");
            entity.Property(claim => claim.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();
            entity.HasIndex(claim => new { claim.TenantId, claim.TransactionId }).IsUnique().HasFilter("status <> 'released'");
            entity.HasIndex(claim => new { claim.TenantId, claim.DependentType, claim.DependentId }).IsUnique().HasFilter("status <> 'released'");
        });

        modelBuilder.Entity<InboxMessage>(entity =>
        {
            entity.ToTable("inbox_messages", "bank_accounts");
            entity.HasKey(message => new { message.ConsumerName, message.EventId });
            entity.Property(message => message.ConsumerName).HasColumnName("consumer_name").HasMaxLength(200);
            entity.Property(message => message.EventId).HasColumnName("event_id");
            entity.Property(message => message.ProcessedAt).HasColumnName("processed_at").IsRequired();
        });

        modelBuilder.Entity<ProjectReplica>(entity =>
        {
            entity.ToTable("project_replica", "bank_accounts");
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

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("outbox_messages", "bank_accounts");
            entity.HasKey(message => message.EventId);
            entity.Property(message => message.EventId).HasColumnName("event_id");
            entity.Property(message => message.EventName).HasColumnName("event_name").HasMaxLength(200).IsRequired();
            entity.Property(message => message.AggregateType).HasColumnName("aggregate_type").HasMaxLength(100).IsRequired();
            entity.Property(message => message.AggregateId).HasColumnName("aggregate_id").IsRequired();
            entity.Property(message => message.AggregateVersion).HasColumnName("aggregate_version").IsRequired();
            entity.Property(message => message.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(message => message.OccurredAt).HasColumnName("occurred_at").IsRequired();
            entity.Property(message => message.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
            entity.Property(message => message.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
            entity.HasIndex(message => new { message.Status, message.OccurredAt });
        });

        modelBuilder.Entity<IdempotencyRecord>(entity =>
        {
            entity.ToTable("idempotency_records", "bank_accounts");
            entity.HasKey(record => new { record.TenantId, record.Operation, record.Key });
            entity.Property(record => record.TenantId).HasColumnName("tenant_id");
            entity.Property(record => record.Operation).HasColumnName("operation").HasMaxLength(100);
            entity.Property(record => record.Key).HasColumnName("key").HasMaxLength(200);
            entity.Property(record => record.Result).HasColumnName("result").HasColumnType("jsonb").IsRequired();
            entity.Property(record => record.CreatedAt).HasColumnName("created_at").IsRequired();
        });
    }
}
