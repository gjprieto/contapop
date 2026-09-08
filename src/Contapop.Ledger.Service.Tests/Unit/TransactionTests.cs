using Contapop.Ledger.Service.Domain.Transactions;

namespace Contapop.Ledger.Service.Tests.Unit;

public sealed class TransactionTests
{
    [Fact]
    public void Archive_marks_the_transaction_archived_without_removing_its_identity()
    {
        var transaction = Transaction.Create(Guid.NewGuid(), Guid.NewGuid(), 1_250, new DateOnly(2026, 9, 8), "expense", null, DateTimeOffset.UtcNow);

        var archived = transaction.TryArchive(1, DateTimeOffset.UtcNow);

        Assert.True(archived);
        Assert.Equal("archived", transaction.Status);
        Assert.Equal(2, transaction.Version);
        Assert.NotEqual(Guid.Empty, transaction.Id);
    }

    [Fact]
    public void Archived_transaction_cannot_be_updated()
    {
        var transaction = Transaction.Create(Guid.NewGuid(), Guid.NewGuid(), 1_250, new DateOnly(2026, 9, 8), "expense", null, DateTimeOffset.UtcNow);
        transaction.TryArchive(1, DateTimeOffset.UtcNow);

        var updated = transaction.TryUpdate(2, null, 2_500, null, null, null, DateTimeOffset.UtcNow);

        Assert.False(updated);
        Assert.Equal(1_250, transaction.AmountMinor);
    }
}
