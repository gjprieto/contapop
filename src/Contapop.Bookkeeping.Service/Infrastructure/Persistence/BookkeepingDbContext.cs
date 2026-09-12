using Contapop.Bookkeeping.Service.Infrastructure.Replication;
using Microsoft.EntityFrameworkCore;

namespace Contapop.Bookkeeping.Service.Infrastructure.Persistence;

public sealed class BookkeepingDbContext(DbContextOptions<BookkeepingDbContext> options) : DbContext(options)
{
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
    public DbSet<ProjectReplica> ProjectReplicas => Set<ProjectReplica>();
    public DbSet<TransactionReplica> TransactionReplicas => Set<TransactionReplica>();

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
    }
}
