namespace Contapop.Identity.Service.Application.Commands.UpdateUserPreferences;

public sealed record UpdateUserPreferencesCommand(
    Guid TenantId,
    Guid UserId,
    int ExpectedVersion,
    string? Theme,
    string? Language,
    bool? NotificationsEnabled);

public sealed record UpdateUserPreferencesResult(
    Guid UserId,
    string Theme,
    string Language,
    bool NotificationsEnabled,
    DateTimeOffset UpdatedAt,
    int Version);
