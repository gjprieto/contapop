namespace Contapop.Identity.Service.Api.Contracts;

public sealed record LoginRequest(string Email, string Password);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record UpdateUserProfileRequest(string Name);

public sealed record UpdateUserPreferencesRequest(string? Theme, string? Language, bool? NotificationsEnabled);
