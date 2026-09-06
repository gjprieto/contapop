namespace Contapop.Identity.Service.Application.Commands.ProvisionTenant;

public sealed record ProvisionTenantCommand(
    string TenantName,
    string OwnerName,
    string OwnerEmail,
    string InitialPassword);

public sealed record ProvisionTenantResult(
    Guid TenantId,
    Guid OwnerUserId,
    Guid ProjectId,
    DateTimeOffset CreatedAt);
