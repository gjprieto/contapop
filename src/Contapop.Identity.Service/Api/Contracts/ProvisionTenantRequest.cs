namespace Contapop.Identity.Service.Api.Contracts;

public sealed record ProvisionTenantRequest(
    string TenantName,
    string OwnerName,
    string OwnerEmail,
    string InitialPassword);

public sealed record ProvisionTenantResponse(
    Guid TenantId,
    Guid OwnerUserId,
    Guid ProjectId,
    DateTimeOffset CreatedAt);
