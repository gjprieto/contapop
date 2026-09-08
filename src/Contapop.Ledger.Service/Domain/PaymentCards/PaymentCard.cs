using Contapop.Ledger.Service.Domain.Events;

namespace Contapop.Ledger.Service.Domain.PaymentCards;

public sealed class PaymentCard
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Label { get; private set; } = null!;
    public string CardholderName { get; private set; } = null!;
    public DateOnly? ExpirationDate { get; private set; }
    public int Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    private List<object> DomainEvents { get; } = [];

    public static PaymentCard Create(Guid tenantId, Guid projectId, string label, string cardholderName, DateOnly? expirationDate, DateTimeOffset now)
    {
        var card = new PaymentCard
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = projectId,
            Label = label,
            CardholderName = cardholderName,
            ExpirationDate = expirationDate,
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now,
        };
        card.DomainEvents.Add(new PaymentCardLinked(card.Id, card.TenantId, card.ProjectId, card.Label, now));
        return card;
    }

    public IReadOnlyCollection<object> GetDomainEvents() => DomainEvents;
    public void ClearDomainEvents() => DomainEvents.Clear();

    public bool HasVersion(int expectedVersion) => Version == expectedVersion;
}
