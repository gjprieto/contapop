namespace Contapop.Billing.Service.Application.Commands;

public sealed record CreateCounterpartyCommand(Guid TenantId, string IdempotencyKey, string Type, string Name, string? TaxId, string? Email, string? Address);
public sealed record UpdateCounterpartyCommand(Guid TenantId, string IdempotencyKey, Guid CounterpartyId, int ExpectedVersion, string? Name, string? TaxId, string? Email, string? Address);
public sealed record ArchiveCounterpartyCommand(Guid TenantId, string IdempotencyKey, Guid CounterpartyId, int ExpectedVersion);
