namespace Contapop.Identity.Service.Application.Commands.UpdateUserProfile;

public sealed record UpdateUserProfileCommand(Guid TenantId, Guid UserId, int ExpectedVersion, string Name);

public sealed record UpdateUserProfileResult(Guid UserId, string Name, DateTimeOffset UpdatedAt, int Version);
