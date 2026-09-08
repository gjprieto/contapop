using System.Text.Json;
using Contapop.Ledger.Service.Domain.BankAccounts;
using Contapop.Ledger.Service.Domain.PaymentCards;
using Contapop.Ledger.Service.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Contapop.Ledger.Service.Application.Commands;

public sealed class AccountCommandHandler(LedgerDbContext database)
{
    public async Task<CommandResult<LinkedBankAccountResult>> LinkBankAccountAsync(LinkBankAccountCommand command, CancellationToken cancellationToken)
    {
        var replay = await GetReplayAsync<LinkedBankAccountResult>(command.TenantId, "link-bank-account", command.IdempotencyKey, cancellationToken);
        if (replay is not null) return CommandResult<LinkedBankAccountResult>.Success(replay);
        if (!await IsActiveProjectAsync(command.TenantId, command.ProjectId, cancellationToken))
        {
            return CommandResult<LinkedBankAccountResult>.ProjectUnavailable();
        }

        var now = DateTimeOffset.UtcNow;
        var account = BankAccount.Create(command.TenantId, command.ProjectId, command.AccountNumber.Trim(), command.BankName.Trim(), now);
        database.BankAccounts.Add(account);
        database.IdempotencyRecords.Add(IdempotencyRecord.Create(command.TenantId, "link-bank-account", command.IdempotencyKey, JsonSerializer.Serialize(new LinkedBankAccountResult(account.Id, account.Status, account.CreatedAt, account.Version)), now));
        await database.SaveChangesAsync(cancellationToken);
        return CommandResult<LinkedBankAccountResult>.Success(new(account.Id, account.Status, account.CreatedAt, account.Version));
    }

    public async Task<CommandResult<ArchivedBankAccountResult>> ArchiveBankAccountAsync(ArchiveBankAccountCommand command, CancellationToken cancellationToken)
    {
        var replay = await GetReplayAsync<ArchivedBankAccountResult>(command.TenantId, "archive-bank-account", command.IdempotencyKey, cancellationToken);
        if (replay is not null) return CommandResult<ArchivedBankAccountResult>.Success(replay);
        var account = await database.BankAccounts.SingleOrDefaultAsync(candidate => candidate.Id == command.BankAccountId && candidate.TenantId == command.TenantId, cancellationToken);
        if (account is null)
        {
            return CommandResult<ArchivedBankAccountResult>.NotFound();
        }

        if (!account.TryArchive(command.ExpectedVersion, DateTimeOffset.UtcNow))
        {
            return CommandResult<ArchivedBankAccountResult>.Conflict();
        }

        var result = new ArchivedBankAccountResult(account.Id, account.Status, account.UpdatedAt, account.Version);
        database.IdempotencyRecords.Add(IdempotencyRecord.Create(command.TenantId, "archive-bank-account", command.IdempotencyKey, JsonSerializer.Serialize(result), DateTimeOffset.UtcNow));
        await database.SaveChangesAsync(cancellationToken);
        return CommandResult<ArchivedBankAccountResult>.Success(result);
    }

    public async Task<CommandResult<LinkedPaymentCardResult>> AddPaymentCardLabelAsync(AddPaymentCardLabelCommand command, CancellationToken cancellationToken)
    {
        var replay = await GetReplayAsync<LinkedPaymentCardResult>(command.TenantId, "add-payment-card-label", command.IdempotencyKey, cancellationToken);
        if (replay is not null) return CommandResult<LinkedPaymentCardResult>.Success(replay);
        if (!await IsActiveProjectAsync(command.TenantId, command.ProjectId, cancellationToken))
        {
            return CommandResult<LinkedPaymentCardResult>.ProjectUnavailable();
        }

        var now = DateTimeOffset.UtcNow;
        var card = PaymentCard.Create(command.TenantId, command.ProjectId, command.Label.Trim(), command.CardholderName.Trim(), command.ExpirationDate, now);
        database.PaymentCards.Add(card);
        database.IdempotencyRecords.Add(IdempotencyRecord.Create(command.TenantId, "add-payment-card-label", command.IdempotencyKey, JsonSerializer.Serialize(new LinkedPaymentCardResult(card.Id, card.Label, card.CreatedAt)), now));
        await database.SaveChangesAsync(cancellationToken);
        return CommandResult<LinkedPaymentCardResult>.Success(new(card.Id, card.Label, card.CreatedAt));
    }

    public async Task<CommandResult> RemovePaymentCardLabelAsync(RemovePaymentCardLabelCommand command, CancellationToken cancellationToken)
    {
        if (await HasReplayAsync(command.TenantId, "remove-payment-card-label", command.IdempotencyKey, cancellationToken)) return CommandResult.Success();
        var card = await database.PaymentCards.SingleOrDefaultAsync(candidate => candidate.Id == command.CardId && candidate.TenantId == command.TenantId, cancellationToken);
        if (card is null)
        {
            return CommandResult.NotFound();
        }

        if (!card.HasVersion(command.ExpectedVersion))
        {
            return CommandResult.Conflict();
        }

        database.PaymentCards.Remove(card);
        database.IdempotencyRecords.Add(IdempotencyRecord.Create(command.TenantId, "remove-payment-card-label", command.IdempotencyKey, JsonSerializer.Serialize(true), DateTimeOffset.UtcNow));
        await database.SaveChangesAsync(cancellationToken);
        return CommandResult.Success();
    }

    private Task<bool> IsActiveProjectAsync(Guid tenantId, Guid projectId, CancellationToken cancellationToken) =>
        database.ProjectReplicas.AnyAsync(project => project.ProjectId == projectId && project.TenantId == tenantId && project.Status == "active", cancellationToken);

    private async Task<T?> GetReplayAsync<T>(Guid tenantId, string operation, string key, CancellationToken cancellationToken) where T : class
    {
        var result = await database.IdempotencyRecords.AsNoTracking()
            .Where(record => record.TenantId == tenantId && record.Operation == operation && record.Key == key)
            .Select(record => record.Result)
            .SingleOrDefaultAsync(cancellationToken);
        return result is null ? null : JsonSerializer.Deserialize<T>(result);
    }

    private Task<bool> HasReplayAsync(Guid tenantId, string operation, string key, CancellationToken cancellationToken) =>
        database.IdempotencyRecords.AnyAsync(record => record.TenantId == tenantId && record.Operation == operation && record.Key == key, cancellationToken);
}

public sealed record LinkedBankAccountResult(Guid BankAccountId, string Status, DateTimeOffset CreatedAt, int Version);
public sealed record ArchivedBankAccountResult(Guid BankAccountId, string Status, DateTimeOffset UpdatedAt, int Version);
public sealed record LinkedPaymentCardResult(Guid CardId, string Label, DateTimeOffset CreatedAt);

public sealed class CommandResult
{
    public bool Succeeded { get; private init; }
    public bool IsNotFound { get; private init; }
    public bool IsConflict { get; private init; }
    public static CommandResult Success() => new() { Succeeded = true };
    public static CommandResult NotFound() => new() { IsNotFound = true };
    public static CommandResult Conflict() => new() { IsConflict = true };
}

public sealed class CommandResult<T>
{
    public T? Value { get; private init; }
    public bool IsProjectUnavailable { get; private init; }
    public bool IsNotFound { get; private init; }
    public bool IsConflict { get; private init; }
    public static CommandResult<T> Success(T value) => new() { Value = value };
    public static CommandResult<T> ProjectUnavailable() => new() { IsProjectUnavailable = true };
    public static CommandResult<T> NotFound() => new() { IsNotFound = true };
    public static CommandResult<T> Conflict() => new() { IsConflict = true };
}
