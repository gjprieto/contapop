namespace Contapop.Identity.Service.Domain.Events;

public sealed record ProjectCreated(Guid ProjectId, Guid TenantId, string Name, string Status, DateTimeOffset OccurredAt) : IDomainEvent;
