namespace Contapop.Identity.Service.Domain.Users;

public sealed class User
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    public string Theme { get; private set; } = null!;
    public string Language { get; private set; } = null!;
    public bool NotificationsEnabled { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static User Create(Guid id, Guid tenantId, string name, string email, DateTimeOffset createdAt) => new()
    {
        Id = id,
        TenantId = tenantId,
        Name = name,
        Email = email,
        Theme = "light",
        Language = "es",
        NotificationsEnabled = true,
        CreatedAt = createdAt,
        UpdatedAt = createdAt,
    };
}
