namespace Contapop.Ledger.Service.Domain.PaymentCards;

public sealed class PaymentCard
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Label { get; private set; } = null!;
    public string CardholderName { get; private set; } = null!;
    public DateOnly? ExpirationDate { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
}
