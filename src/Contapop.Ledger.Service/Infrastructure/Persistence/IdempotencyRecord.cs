namespace Contapop.Ledger.Service.Infrastructure.Persistence;

public sealed class IdempotencyRecord
{
    public Guid TenantId { get; private set; }
    public string Operation { get; private set; } = null!;
    public string Key { get; private set; } = null!;
    public string Result { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }

    public static IdempotencyRecord Create(Guid tenantId, string operation, string key, string result, DateTimeOffset now) => new()
    {
        TenantId = tenantId,
        Operation = operation,
        Key = key,
        Result = result,
        CreatedAt = now,
    };
}
