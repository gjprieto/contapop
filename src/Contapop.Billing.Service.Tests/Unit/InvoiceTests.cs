using Contapop.Billing.Service.Infrastructure.Persistence;

namespace Contapop.Billing.Service.Tests.Unit;

public sealed class InvoiceTests
{
    [Theory]
    [InlineData(10, 0.25, 2)]
    [InlineData(14, 0.25, 4)]
    [InlineData(99, 0.21, 21)]
    public void Create_calculates_vat_using_bankers_rounding(long netAmountMinor, decimal taxRate, long expectedTaxAmountMinor)
    {
        var invoice = Invoice.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "outgoing", netAmountMinor, taxRate, new DateOnly(2026, 9, 9), new DateOnly(2026, 10, 9), DateTimeOffset.UtcNow);

        Assert.Equal(expectedTaxAmountMinor, invoice.TaxAmountMinor);
        Assert.Equal(netAmountMinor + expectedTaxAmountMinor, invoice.TotalAmountMinor);
    }

    [Fact]
    public void Void_only_accepts_a_draft_at_the_expected_version()
    {
        var invoice = Invoice.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "incoming", 100, 0.21m, new DateOnly(2026, 9, 9), new DateOnly(2026, 10, 9), DateTimeOffset.UtcNow);

        Assert.True(invoice.TryVoid(1, DateTimeOffset.UtcNow));
        Assert.False(invoice.TryIssue(2, DateTimeOffset.UtcNow));
    }
}
