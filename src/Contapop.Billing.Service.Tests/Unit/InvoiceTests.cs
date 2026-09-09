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

    [Fact]
    public void Mark_paid_transitions_an_issued_invoice_once()
    {
        var invoice = Invoice.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "outgoing", 100, 0.21m, new DateOnly(2026, 9, 9), new DateOnly(2026, 10, 9), DateTimeOffset.UtcNow);
        Assert.True(invoice.TryIssue(1, DateTimeOffset.UtcNow));

        Assert.True(invoice.TryMarkPaid(DateTimeOffset.UtcNow));
        Assert.Equal("paid", invoice.Status);
        Assert.False(invoice.TryMarkPaid(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Mark_overdue_transitions_only_an_unpaid_issued_invoice_past_its_due_date()
    {
        var now = new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);
        var overdueInvoice = Invoice.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "outgoing", 100, 0.21m, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 9), now);
        var paidInvoice = Invoice.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "outgoing", 100, 0.21m, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 9), now);
        Assert.True(paidInvoice.TryIssue(1, now));
        Assert.True(paidInvoice.TryMarkPaid(now));
        Assert.True(overdueInvoice.TryIssue(1, now));

        Assert.True(overdueInvoice.TryMarkOverdue(DateOnly.FromDateTime(now.UtcDateTime), now));
        Assert.Equal("overdue", overdueInvoice.Status);
        Assert.False(paidInvoice.TryMarkOverdue(DateOnly.FromDateTime(now.UtcDateTime), now));
        Assert.Equal("paid", paidInvoice.Status);
    }
}
