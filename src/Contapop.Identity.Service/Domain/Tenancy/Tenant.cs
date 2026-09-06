using Contapop.Identity.Service.Domain.Events;

namespace Contapop.Identity.Service.Domain.Tenancy;

public sealed class Tenant
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;

    public static Tenant Create(Guid id, string name, DateTimeOffset createdAt) => new()
    {
        Id = id,
        Name = name,
        CreatedAt = createdAt,
        UpdatedAt = createdAt,
    };

    public void MarkProvisioned(Guid ownerUserId) => _domainEvents.Add(new TenantProvisioned(Id, Name, ownerUserId, CreatedAt));

    public void ClearDomainEvents() => _domainEvents.Clear();
}
