using Contapop.Identity.Service.Domain.Events;

namespace Contapop.Identity.Service.Domain.Tenancy;

public sealed class Project
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = null!;
    public string Status { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;

    public static Project Create(Guid id, Guid tenantId, string name, DateTimeOffset createdAt)
    {
        var project = new Project
        {
            Id = id,
            TenantId = tenantId,
            Name = name,
            Status = "active",
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
        };
        project._domainEvents.Add(new ProjectCreated(id, tenantId, name, project.Status, createdAt));
        return project;
    }

    public void ClearDomainEvents() => _domainEvents.Clear();
}
