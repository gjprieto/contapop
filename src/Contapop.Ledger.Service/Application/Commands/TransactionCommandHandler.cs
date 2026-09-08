using System.Text.Json;
using Contapop.Ledger.Service.Domain.Transactions;
using Contapop.Ledger.Service.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Contapop.Ledger.Service.Application.Commands;

public sealed class TransactionCommandHandler(LedgerDbContext database)
{
    public async Task<TransactionCommandResult<RecordedTransactionResult>> RecordTransactionAsync(RecordTransactionCommand command, CancellationToken cancellationToken)
    {
        var replay = await GetReplayAsync<RecordedTransactionResult>(command.TenantId, "record-transaction", command.IdempotencyKey, cancellationToken);
        if (replay is not null) return TransactionCommandResult<RecordedTransactionResult>.Success(replay);
        if (!await HasActiveBankAccountAsync(command.TenantId, command.BankAccountId, cancellationToken)) return TransactionCommandResult<RecordedTransactionResult>.BankAccountUnavailable();

        var now = DateTimeOffset.UtcNow;
        var transaction = Transaction.Create(command.TenantId, command.BankAccountId, command.AmountMinor, command.Date, command.Type, now);
        var result = new RecordedTransactionResult(transaction.Id, transaction.Status, transaction.CreatedAt, (int)transaction.Version);
        database.Transactions.Add(transaction);
        database.IdempotencyRecords.Add(IdempotencyRecord.Create(command.TenantId, "record-transaction", command.IdempotencyKey, JsonSerializer.Serialize(result), now));
        await database.SaveChangesAsync(cancellationToken);
        return TransactionCommandResult<RecordedTransactionResult>.Success(result);
    }

    public async Task<TransactionCommandResult<TransactionDetailsResult>> UpdateTransactionAsync(UpdateTransactionCommand command, CancellationToken cancellationToken)
    {
        var replay = await GetReplayAsync<TransactionDetailsResult>(command.TenantId, "update-transaction", command.IdempotencyKey, cancellationToken);
        if (replay is not null) return TransactionCommandResult<TransactionDetailsResult>.Success(replay);
        var transaction = await database.Transactions.SingleOrDefaultAsync(candidate => candidate.Id == command.TransactionId && candidate.TenantId == command.TenantId, cancellationToken);
        if (transaction is null) return TransactionCommandResult<TransactionDetailsResult>.NotFound();
        if (command.BankAccountId is { } bankAccountId && !await HasActiveBankAccountAsync(command.TenantId, bankAccountId, cancellationToken)) return TransactionCommandResult<TransactionDetailsResult>.BankAccountUnavailable();
        if (!transaction.TryUpdate(command.ExpectedVersion, command.BankAccountId, command.AmountMinor, command.Date, command.Type, DateTimeOffset.UtcNow)) return TransactionCommandResult<TransactionDetailsResult>.Conflict();

        var result = ToDetails(transaction);
        database.IdempotencyRecords.Add(IdempotencyRecord.Create(command.TenantId, "update-transaction", command.IdempotencyKey, JsonSerializer.Serialize(result), DateTimeOffset.UtcNow));
        await database.SaveChangesAsync(cancellationToken);
        return TransactionCommandResult<TransactionDetailsResult>.Success(result);
    }

    public async Task<TransactionCommandResult<ArchivedTransactionResult>> ArchiveTransactionAsync(ArchiveTransactionCommand command, CancellationToken cancellationToken)
    {
        var replay = await GetReplayAsync<ArchivedTransactionResult>(command.TenantId, "archive-transaction", command.IdempotencyKey, cancellationToken);
        if (replay is not null) return TransactionCommandResult<ArchivedTransactionResult>.Success(replay);
        var transaction = await database.Transactions.SingleOrDefaultAsync(candidate => candidate.Id == command.TransactionId && candidate.TenantId == command.TenantId, cancellationToken);
        if (transaction is null) return TransactionCommandResult<ArchivedTransactionResult>.NotFound();
        if (!transaction.TryArchive(command.ExpectedVersion, DateTimeOffset.UtcNow)) return TransactionCommandResult<ArchivedTransactionResult>.Conflict();

        var result = new ArchivedTransactionResult(transaction.Id, transaction.Status, transaction.UpdatedAt, (int)transaction.Version);
        database.IdempotencyRecords.Add(IdempotencyRecord.Create(command.TenantId, "archive-transaction", command.IdempotencyKey, JsonSerializer.Serialize(result), DateTimeOffset.UtcNow));
        await database.SaveChangesAsync(cancellationToken);
        return TransactionCommandResult<ArchivedTransactionResult>.Success(result);
    }

    private Task<bool> HasActiveBankAccountAsync(Guid tenantId, Guid bankAccountId, CancellationToken cancellationToken) =>
        database.BankAccounts.AnyAsync(account => account.Id == bankAccountId && account.TenantId == tenantId && account.Status == "active", cancellationToken);

    private async Task<T?> GetReplayAsync<T>(Guid tenantId, string operation, string key, CancellationToken cancellationToken) where T : class
    {
        var result = await database.IdempotencyRecords.AsNoTracking()
            .Where(record => record.TenantId == tenantId && record.Operation == operation && record.Key == key)
            .Select(record => record.Result)
            .SingleOrDefaultAsync(cancellationToken);
        return result is null ? null : JsonSerializer.Deserialize<T>(result);
    }

    private static TransactionDetailsResult ToDetails(Transaction transaction) => new(transaction.Id, transaction.BankAccountId, transaction.AmountMinor, transaction.Date, transaction.Type, transaction.Status, transaction.CreatedAt, transaction.UpdatedAt, (int)transaction.Version);
}

public sealed record RecordedTransactionResult(Guid TransactionId, string Status, DateTimeOffset CreatedAt, int Version);
public sealed record ArchivedTransactionResult(Guid TransactionId, string Status, DateTimeOffset UpdatedAt, int Version);
public sealed record TransactionDetailsResult(Guid TransactionId, Guid BankAccountId, long AmountMinor, DateOnly Date, string Type, string Status, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, int Version);

public sealed class TransactionCommandResult<T>
{
    public T? Value { get; private init; }
    public bool IsBankAccountUnavailable { get; private init; }
    public bool IsNotFound { get; private init; }
    public bool IsConflict { get; private init; }
    public static TransactionCommandResult<T> Success(T value) => new() { Value = value };
    public static TransactionCommandResult<T> BankAccountUnavailable() => new() { IsBankAccountUnavailable = true };
    public static TransactionCommandResult<T> NotFound() => new() { IsNotFound = true };
    public static TransactionCommandResult<T> Conflict() => new() { IsConflict = true };
}
