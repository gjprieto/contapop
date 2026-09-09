namespace Contapop.Billing.Service.Infrastructure.Replication;

public sealed class ProjectReplica
{
    public Guid ProjectId { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = null!;
    public string Status { get; private set; } = null!;
    public long AggregateVersion { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static ProjectReplica Create(Guid projectId, Guid tenantId, string name, string status, long aggregateVersion, DateTimeOffset createdAt) => new()
    {
        ProjectId = projectId, TenantId = tenantId, Name = name, Status = status,
        AggregateVersion = aggregateVersion, CreatedAt = createdAt, UpdatedAt = createdAt,
    };

    public void Apply(string? name, string status, long aggregateVersion, DateTimeOffset updatedAt)
    {
        if (aggregateVersion <= AggregateVersion) return;
        if (name is not null) Name = name;
        Status = status;
        AggregateVersion = aggregateVersion;
        UpdatedAt = updatedAt;
    }
}
