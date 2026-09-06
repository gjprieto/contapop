namespace Contapop.Identity.Service.Domain.Events;

public sealed record TenantProvisioned(Guid TenantId, string Name, Guid OwnerUserId, DateTimeOffset OccurredAt) : IDomainEvent;
