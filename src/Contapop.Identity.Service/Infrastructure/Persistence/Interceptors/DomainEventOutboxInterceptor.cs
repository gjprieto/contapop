using System.Text.Json;
using Contapop.Identity.Service.Domain.Events;
using Contapop.Identity.Service.Domain.Tenancy;
using Contapop.Identity.Service.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Contapop.Identity.Service.Infrastructure.Persistence.Interceptors;

public sealed class DomainEventOutboxInterceptor : SaveChangesInterceptor
{
    private static void AddOutboxMessages(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (var aggregate in context.ChangeTracker.Entries()
                     .Select(entry => entry.Entity)
                     .OfType<Tenant>()
                     .ToArray())
        {
            foreach (var domainEvent in aggregate.DomainEvents.OfType<TenantProvisioned>())
            {
                context.Set<OutboxMessage>().Add(OutboxMessage.Create("identity.tenant-created.v1", "Tenant", domainEvent.TenantId, domainEvent.TenantId, domainEvent.OccurredAt, JsonSerializer.Serialize(new { tenant_id = domainEvent.TenantId, name = domainEvent.Name, owner_user_id = domainEvent.OwnerUserId, created_at = domainEvent.OccurredAt })));
            }
            aggregate.ClearDomainEvents();
        }

        foreach (var aggregate in context.ChangeTracker.Entries()
                     .Select(entry => entry.Entity)
                     .OfType<Project>()
                     .ToArray())
        {
            foreach (var domainEvent in aggregate.DomainEvents.OfType<ProjectCreated>())
            {
                context.Set<OutboxMessage>().Add(OutboxMessage.Create("identity.project-created.v1", "Project", domainEvent.ProjectId, domainEvent.TenantId, domainEvent.OccurredAt, JsonSerializer.Serialize(new { project_id = domainEvent.ProjectId, tenant_id = domainEvent.TenantId, name = domainEvent.Name, status = domainEvent.Status, created_at = domainEvent.OccurredAt })));
            }
            aggregate.ClearDomainEvents();
        }
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        AddOutboxMessages(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        AddOutboxMessages(eventData.Context);
        return base.SavingChanges(eventData, result);
    }
}
