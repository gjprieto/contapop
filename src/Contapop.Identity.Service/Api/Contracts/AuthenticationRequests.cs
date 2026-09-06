namespace Contapop.Identity.Service.Api.Contracts;

public sealed record LoginRequest(string Email, string Password);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
