using System.Text.Json;
using Contapop.Billing.Service.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Contapop.Billing.Service.Application.Commands;

public sealed class CounterpartyCommandHandler(BillingDbContext database)
{
    public async Task<CounterpartyCommandResult<CounterpartyResponse>> CreateAsync(CreateCounterpartyCommand command, CancellationToken cancellationToken)
    {
        var replay = await GetReplayAsync<CounterpartyResponse>(command.TenantId, "create-counterparty", command.IdempotencyKey, cancellationToken);
        if (replay is not null) return CounterpartyCommandResult<CounterpartyResponse>.Success(replay);

        var now = DateTimeOffset.UtcNow;
        var counterparty = Counterparty.Create(command.TenantId, command.Type, command.Name, command.TaxId, command.Email, command.Address, now);
        database.Counterparties.Add(counterparty);
        var result = ToResponse(counterparty);
        database.IdempotencyRecords.Add(IdempotencyRecord.Create(command.TenantId, "create-counterparty", command.IdempotencyKey, JsonSerializer.Serialize(result), now));
        await database.SaveChangesAsync(cancellationToken);
        return CounterpartyCommandResult<CounterpartyResponse>.Success(result);
    }

    public async Task<CounterpartyCommandResult<CounterpartyResponse>> UpdateAsync(UpdateCounterpartyCommand command, CancellationToken cancellationToken)
    {
        var replay = await GetReplayAsync<CounterpartyResponse>(command.TenantId, "update-counterparty", command.IdempotencyKey, cancellationToken);
        if (replay is not null) return CounterpartyCommandResult<CounterpartyResponse>.Success(replay);
        var counterparty = await database.Counterparties.SingleOrDefaultAsync(item => item.Id == command.CounterpartyId && item.TenantId == command.TenantId, cancellationToken);
        if (counterparty is null) return CounterpartyCommandResult<CounterpartyResponse>.NotFound();
        if (!counterparty.TryUpdate(command.ExpectedVersion, command.Name, command.TaxId, command.Email, command.Address, DateTimeOffset.UtcNow)) return CounterpartyCommandResult<CounterpartyResponse>.Conflict();

        var result = ToResponse(counterparty);
        database.IdempotencyRecords.Add(IdempotencyRecord.Create(command.TenantId, "update-counterparty", command.IdempotencyKey, JsonSerializer.Serialize(result), DateTimeOffset.UtcNow));
        await database.SaveChangesAsync(cancellationToken);
        return CounterpartyCommandResult<CounterpartyResponse>.Success(result);
    }

    public async Task<CounterpartyCommandResult<ArchivedCounterpartyResponse>> ArchiveAsync(ArchiveCounterpartyCommand command, CancellationToken cancellationToken)
    {
        var replay = await GetReplayAsync<ArchivedCounterpartyResponse>(command.TenantId, "archive-counterparty", command.IdempotencyKey, cancellationToken);
        if (replay is not null) return CounterpartyCommandResult<ArchivedCounterpartyResponse>.Success(replay);
        var counterparty = await database.Counterparties.SingleOrDefaultAsync(item => item.Id == command.CounterpartyId && item.TenantId == command.TenantId, cancellationToken);
        if (counterparty is null) return CounterpartyCommandResult<ArchivedCounterpartyResponse>.NotFound();
        if (!counterparty.TryArchive(command.ExpectedVersion, DateTimeOffset.UtcNow)) return CounterpartyCommandResult<ArchivedCounterpartyResponse>.Conflict();

        var result = new ArchivedCounterpartyResponse(counterparty.Id, counterparty.Status, counterparty.UpdatedAt, (int)counterparty.Version);
        database.IdempotencyRecords.Add(IdempotencyRecord.Create(command.TenantId, "archive-counterparty", command.IdempotencyKey, JsonSerializer.Serialize(result), DateTimeOffset.UtcNow));
        await database.SaveChangesAsync(cancellationToken);
        return CounterpartyCommandResult<ArchivedCounterpartyResponse>.Success(result);
    }

    private async Task<T?> GetReplayAsync<T>(Guid tenantId, string operation, string key, CancellationToken cancellationToken) where T : class
    {
        var result = await database.IdempotencyRecords.AsNoTracking().Where(record => record.TenantId == tenantId && record.Operation == operation && record.Key == key).Select(record => record.Result).SingleOrDefaultAsync(cancellationToken);
        return result is null ? null : JsonSerializer.Deserialize<T>(result);
    }

    private static CounterpartyResponse ToResponse(Counterparty counterparty) => new(counterparty.Id, counterparty.Type, counterparty.Name, counterparty.TaxId, counterparty.Email, counterparty.Address, counterparty.Status, counterparty.CreatedAt, counterparty.UpdatedAt, (int)counterparty.Version);
}

public sealed record CounterpartyResponse(Guid CounterpartyId, string Type, string Name, string? TaxId, string? Email, string? Address, string Status, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, int Version);
public sealed record ArchivedCounterpartyResponse(Guid CounterpartyId, string Status, DateTimeOffset UpdatedAt, int Version);

public sealed class CounterpartyCommandResult<T> where T : class
{
    public T? Value { get; private init; }
    public bool IsNotFound { get; private init; }
    public bool IsConflict { get; private init; }
    public static CounterpartyCommandResult<T> Success(T value) => new() { Value = value };
    public static CounterpartyCommandResult<T> NotFound() => new() { IsNotFound = true };
    public static CounterpartyCommandResult<T> Conflict() => new() { IsConflict = true };
}
