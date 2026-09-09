using Contapop.Billing.Service.Infrastructure.Persistence;

namespace Contapop.Billing.Service.Tests.Unit;

public sealed class CounterpartyTests
{
    [Fact]
    public void Update_changes_supplied_fields_and_increments_version()
    {
        var createdAt = new DateTimeOffset(2026, 9, 9, 10, 0, 0, TimeSpan.Zero);
        var counterparty = Counterparty.Create(Guid.NewGuid(), "customer", "Original", null, null, null, createdAt);

        var updated = counterparty.TryUpdate(1, "Updated", "ESB123", null, "Calle Mayor", createdAt.AddMinutes(1));

        Assert.True(updated);
        Assert.Equal("Updated", counterparty.Name);
        Assert.Equal("ESB123", counterparty.TaxId);
        Assert.Equal("Calle Mayor", counterparty.Address);
        Assert.Equal(2, counterparty.Version);
    }

    [Fact]
    public void Archive_keeps_counterparty_and_prevents_further_updates()
    {
        var counterparty = Counterparty.Create(Guid.NewGuid(), "supplier", "Supplier", null, null, null, DateTimeOffset.UtcNow);

        var archived = counterparty.TryArchive(1, DateTimeOffset.UtcNow);

        Assert.True(archived);
        Assert.Equal("archived", counterparty.Status);
        Assert.False(counterparty.TryUpdate(2, "Renamed", null, null, null, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Stale_version_cannot_archive_counterparty()
    {
        var counterparty = Counterparty.Create(Guid.NewGuid(), "supplier", "Supplier", null, null, null, DateTimeOffset.UtcNow);

        Assert.False(counterparty.TryArchive(2, DateTimeOffset.UtcNow));
        Assert.Equal("active", counterparty.Status);
        Assert.Equal(1, counterparty.Version);
    }
}
