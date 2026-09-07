using Contapop.Identity.Service.Domain.Tenancy;
using Contapop.Identity.Service.Domain.Users;
using Contapop.Identity.Service.Infrastructure.Outbox;
using Contapop.Identity.Service.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Contapop.Identity.Service.Infrastructure.Persistence;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : IdentityDbContext<IdentityCredential, Microsoft.AspNetCore.Identity.IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<User> DomainUsers => Set<User>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("tenancy");

        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.ToTable("tenants", "tenancy");
            entity.HasKey(tenant => tenant.Id);
            entity.Property(tenant => tenant.Id).HasColumnName("id");
            entity.Property(tenant => tenant.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            entity.Property(tenant => tenant.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(tenant => tenant.UpdatedAt).HasColumnName("updated_at").IsRequired();
        });

        modelBuilder.Entity<Project>(entity =>
        {
            entity.ToTable("projects", "tenancy");
            entity.HasKey(project => project.Id);
            entity.Property(project => project.Id).HasColumnName("id");
            entity.Property(project => project.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(project => project.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            entity.Property(project => project.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
            entity.Property(project => project.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(project => project.UpdatedAt).HasColumnName("updated_at").IsRequired();
            entity.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(project => project.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(project => project.TenantId);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users", "users");
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Id).HasColumnName("id");
            entity.Property(user => user.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(user => user.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            entity.Property(user => user.Email).HasColumnName("email").HasMaxLength(320).IsRequired();
            entity.Property(user => user.Theme).HasColumnName("theme").HasMaxLength(20).IsRequired();
            entity.Property(user => user.Language).HasColumnName("language").HasMaxLength(10).IsRequired();
            entity.Property(user => user.NotificationsEnabled).HasColumnName("notifications_enabled").IsRequired();
            entity.Property(user => user.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();
            entity.Property(user => user.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(user => user.UpdatedAt).HasColumnName("updated_at").IsRequired();
            entity.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(user => user.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(user => user.Email).IsUnique();
            entity.HasIndex(user => user.TenantId);
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("outbox_messages", "tenancy");
            entity.HasKey(message => message.EventId);
            entity.Property(message => message.EventId).HasColumnName("event_id");
            entity.Property(message => message.EventName).HasColumnName("event_name").HasMaxLength(200).IsRequired();
            entity.Property(message => message.AggregateType).HasColumnName("aggregate_type").HasMaxLength(100).IsRequired();
            entity.Property(message => message.AggregateId).HasColumnName("aggregate_id").IsRequired();
            entity.Property(message => message.AggregateVersion).HasColumnName("aggregate_version").IsRequired();
            entity.Property(message => message.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(message => message.ActorId).HasColumnName("actor_id");
            entity.Property(message => message.CorrelationId).HasColumnName("correlation_id");
            entity.Property(message => message.CausationId).HasColumnName("causation_id");
            entity.Property(message => message.OccurredAt).HasColumnName("occurred_at").IsRequired();
            entity.Property(message => message.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
            entity.Property(message => message.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
            entity.Property(message => message.PublishAttempts).HasColumnName("publish_attempts").IsRequired();
            entity.Property(message => message.PublishedAt).HasColumnName("published_at");
            entity.Property(message => message.LastError).HasColumnName("last_error").HasMaxLength(4000);
            entity.Property(message => message.LockedUntil).HasColumnName("locked_until");
            entity.HasIndex(message => new { message.Status, message.OccurredAt });
        });

        modelBuilder.Entity<IdentityCredential>(entity =>
        {
            entity.ToTable("identity_credentials", "users");
            entity.Property(credential => credential.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(credential => credential.DomainUserId).HasColumnName("domain_user_id").IsRequired();
            entity.HasIndex(credential => credential.DomainUserId).IsUnique();
            entity.HasIndex(credential => new { credential.TenantId, credential.NormalizedEmail }).IsUnique();
        });

        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityRole<Guid>>().ToTable("identity_roles", "users");
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserRole<Guid>>().ToTable("identity_user_roles", "users");
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserClaim<Guid>>().ToTable("identity_user_claims", "users");
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserLogin<Guid>>().ToTable("identity_user_logins", "users");
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityRoleClaim<Guid>>().ToTable("identity_role_claims", "users");
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserToken<Guid>>().ToTable("identity_user_tokens", "users");
    }
}
