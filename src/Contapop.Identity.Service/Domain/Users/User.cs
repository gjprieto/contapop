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
    public int Version { get; private set; }
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
        Version = 1,
        CreatedAt = createdAt,
        UpdatedAt = createdAt,
    };

    public void UpdateProfile(string name, DateTimeOffset updatedAt)
    {
        Name = name;
        Version++;
        UpdatedAt = updatedAt;
    }

    public void UpdatePreferences(
        string? theme,
        string? language,
        bool? notificationsEnabled,
        DateTimeOffset updatedAt)
    {
        if (theme is not null)
        {
            Theme = theme;
        }

        if (language is not null)
        {
            Language = language;
        }

        if (notificationsEnabled is not null)
        {
            NotificationsEnabled = notificationsEnabled.Value;
        }

        Version++;
        UpdatedAt = updatedAt;
    }
}
