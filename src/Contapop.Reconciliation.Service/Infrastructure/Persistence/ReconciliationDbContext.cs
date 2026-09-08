using Microsoft.EntityFrameworkCore;

namespace Contapop.Reconciliation.Service.Infrastructure.Persistence;

public sealed class ReconciliationDbContext(DbContextOptions<ReconciliationDbContext> options) : DbContext(options)
{
    public DbSet<ReconciliationOperation> Operations => Set<ReconciliationOperation>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ReconciliationOperation>(entity =>
        {
            entity.ToTable("reconciliation_operations", "reconciliation"); entity.HasKey(operation => operation.Id);
            entity.Property(operation => operation.Id).HasColumnName("id"); entity.Property(operation => operation.TenantId).HasColumnName("tenant_id");
            entity.Property(operation => operation.TransactionId).HasColumnName("transaction_id"); entity.Property(operation => operation.DependentType).HasColumnName("dependent_type").HasMaxLength(20);
            entity.Property(operation => operation.DependentId).HasColumnName("dependent_id"); entity.Property(operation => operation.ClaimId).HasColumnName("claim_id");
            entity.Property(operation => operation.Status).HasColumnName("status").HasMaxLength(30); entity.Property(operation => operation.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(200);
            entity.Property(operation => operation.Attempts).HasColumnName("attempts"); entity.Property(operation => operation.LastError).HasColumnName("last_error").HasMaxLength(4000);
            entity.Property(operation => operation.NextAttemptAt).HasColumnName("next_attempt_at"); entity.Property(operation => operation.CreatedAt).HasColumnName("created_at"); entity.Property(operation => operation.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(operation => new { operation.TenantId, operation.IdempotencyKey }).IsUnique(); entity.HasIndex(operation => new { operation.Status, operation.NextAttemptAt });
        });
    }
}
public sealed class ReconciliationOperation
{
    public Guid Id { get; private set; } public Guid TenantId { get; private set; } public Guid TransactionId { get; private set; }
    public string DependentType { get; private set; } = null!; public Guid DependentId { get; private set; } public Guid? ClaimId { get; private set; }
    public string Status { get; private set; } = null!; public string IdempotencyKey { get; private set; } = null!; public int Attempts { get; private set; }
    public string? LastError { get; private set; } public DateTimeOffset? NextAttemptAt { get; private set; } public DateTimeOffset CreatedAt { get; private set; } public DateTimeOffset UpdatedAt { get; private set; }
}
