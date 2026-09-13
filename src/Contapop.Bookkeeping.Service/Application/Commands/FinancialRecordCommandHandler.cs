using System.Text.Json;
using Contapop.Bookkeeping.Service.Application.Abstractions;
using Contapop.Bookkeeping.Service.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Contapop.Bookkeeping.Service.Application.Commands;

public sealed class FinancialRecordCommandHandler(BookkeepingDbContext database, IReconciliationClaimValidator claimValidator, IDocumentExtractionAdapter extractionAdapter)
{
    public Task<RecordResult<FinancialRecordResponse>> CreateExpenseAsync(CreateFinancialRecordCommand command, CancellationToken ct) => CreateAsync<Expense>(command, "create-expense", "bookkeeping.expense-recorded.v1", "Expense", "expense_id", ct);
    public Task<RecordResult<FinancialRecordResponse>> CreateRevenueAsync(CreateFinancialRecordCommand command, CancellationToken ct) => CreateAsync<Revenue>(command, "create-revenue", "bookkeeping.revenue-recorded.v1", "Revenue", "revenue_id", ct);
    public Task<RecordResult<FinancialRecordResponse>> UpdateExpenseAsync(UpdateFinancialRecordCommand command, CancellationToken ct) => UpdateAsync<Expense>(command, "update-expense", ct);
    public Task<RecordResult<FinancialRecordResponse>> UpdateRevenueAsync(UpdateFinancialRecordCommand command, CancellationToken ct) => UpdateAsync<Revenue>(command, "update-revenue", ct);
    public Task<RecordResult<DeletedFinancialRecordResponse>> DeleteExpenseAsync(DeleteFinancialRecordCommand command, CancellationToken ct) => DeleteAsync<Expense>(command, "delete-expense", ct);
    public Task<RecordResult<DeletedFinancialRecordResponse>> DeleteRevenueAsync(DeleteFinancialRecordCommand command, CancellationToken ct) => DeleteAsync<Revenue>(command, "delete-revenue", ct);
    public Task<RecordResult<FinancialRecordResponse>> ReconcileExpenseAsync(ReconcileFinancialRecordCommand command, CancellationToken ct) => ReconcileAsync<Expense>(command, "reconcile-expense", "expense", ct);
    public Task<RecordResult<FinancialRecordResponse>> ReconcileRevenueAsync(ReconcileFinancialRecordCommand command, CancellationToken ct) => ReconcileAsync<Revenue>(command, "reconcile-revenue", "revenue", ct);
    public Task<RecordResult<ImportedFinancialRecordResponse>> ImportExpenseAsync(ImportFinancialRecordCommand command, CancellationToken ct) => ImportAsync<Expense>(command, "import-expense", ct);
    public Task<RecordResult<ImportedFinancialRecordResponse>> ImportRevenueAsync(ImportFinancialRecordCommand command, CancellationToken ct) => ImportAsync<Revenue>(command, "import-revenue", ct);
    public Task<RecordResult<FinancialRecordResponse>> ConfirmExpenseAsync(ConfirmImportedFinancialRecordCommand command, CancellationToken ct) => ConfirmAsync<Expense>(command, "confirm-expense", "bookkeeping.expense-recorded.v1", "Expense", "expense_id", ct);
    public Task<RecordResult<FinancialRecordResponse>> ConfirmRevenueAsync(ConfirmImportedFinancialRecordCommand command, CancellationToken ct) => ConfirmAsync<Revenue>(command, "confirm-revenue", "bookkeeping.revenue-recorded.v1", "Revenue", "revenue_id", ct);

    private async Task<RecordResult<FinancialRecordResponse>> CreateAsync<T>(CreateFinancialRecordCommand command, string operation, string eventName, string aggregateType, string idName, CancellationToken ct) where T : FinancialRecord
    {
        var replay = await ReplayAsync<FinancialRecordResponse>(command.TenantId, operation, command.IdempotencyKey, ct);
        if (replay is not null) return RecordResult<FinancialRecordResponse>.Success(replay);
        if (!await database.ProjectReplicas.AnyAsync(project => project.ProjectId == command.ProjectId && project.TenantId == command.TenantId && project.Status == "active", ct)) return RecordResult<FinancialRecordResponse>.ProjectUnavailable();
        var now = DateTimeOffset.UtcNow;
        var record = typeof(T) == typeof(Expense) ? (FinancialRecord)Expense.Create(command.TenantId, command.ProjectId, command.AmountMinor, command.Date, command.Category, command.Recurring, command.RecurringInterval, now) : Revenue.Create(command.TenantId, command.ProjectId, command.AmountMinor, command.Date, command.Category, command.Recurring, command.RecurringInterval, now);
        database.Add(record);
        var response = ToResponse(record);
        database.OutboxMessages.Add(OutboxMessage.Create(eventName, aggregateType, record.Id, record.Version, record.TenantId, now, JsonSerializer.Serialize(new Dictionary<string, object?> { [idName] = record.Id, ["project_id"] = record.ProjectId, ["amount_minor"] = record.AmountMinor, ["date"] = record.Date, ["category"] = record.Category, ["recurring"] = record.Recurring, ["recurring_interval"] = record.RecurringInterval, ["recorded_at"] = now })));
        database.IdempotencyRecords.Add(IdempotencyRecord.Create(command.TenantId, operation, command.IdempotencyKey, JsonSerializer.Serialize(response), now));
        await database.SaveChangesAsync(ct);
        return RecordResult<FinancialRecordResponse>.Success(response);
    }

    private async Task<RecordResult<FinancialRecordResponse>> UpdateAsync<T>(UpdateFinancialRecordCommand command, string operation, CancellationToken ct) where T : FinancialRecord
    {
        var replay = await ReplayAsync<FinancialRecordResponse>(command.TenantId, operation, command.IdempotencyKey, ct);
        if (replay is not null) return RecordResult<FinancialRecordResponse>.Success(replay);
        var record = await database.Set<T>().SingleOrDefaultAsync(item => item.Id == command.RecordId && item.TenantId == command.TenantId, ct);
        if (record is null) return RecordResult<FinancialRecordResponse>.NotFound();
        if (!record.TryUpdate(command.AmountMinor, command.Date, command.Category, command.Recurring, command.RecurringInterval, command.UpdateRecurringInterval, command.ExpectedVersion, DateTimeOffset.UtcNow)) return RecordResult<FinancialRecordResponse>.Conflict();
        var response = ToResponse(record);
        database.IdempotencyRecords.Add(IdempotencyRecord.Create(command.TenantId, operation, command.IdempotencyKey, JsonSerializer.Serialize(response), record.UpdatedAt));
        await database.SaveChangesAsync(ct);
        return RecordResult<FinancialRecordResponse>.Success(response);
    }

    private async Task<RecordResult<ImportedFinancialRecordResponse>> ImportAsync<T>(ImportFinancialRecordCommand command, string operation, CancellationToken ct) where T : FinancialRecord
    {
        var replay = await ReplayAsync<ImportedFinancialRecordResponse>(command.TenantId, operation, command.IdempotencyKey, ct);
        if (replay is not null) return RecordResult<ImportedFinancialRecordResponse>.Success(replay);
        if (!await database.ProjectReplicas.AnyAsync(project => project.ProjectId == command.ProjectId && project.TenantId == command.TenantId && project.Status == "active", ct)) return RecordResult<ImportedFinancialRecordResponse>.ProjectUnavailable();
        DocumentExtractionResult extracted;
        try { extracted = await extractionAdapter.ExtractAsync(command.Document, ct); }
        catch (DocumentExtractionException) { return RecordResult<ImportedFinancialRecordResponse>.ExtractionUnavailable(); }
        var now = DateTimeOffset.UtcNow;
        var record = typeof(T) == typeof(Expense) ? (FinancialRecord)Expense.Import(command.TenantId, command.ProjectId, extracted, now) : Revenue.Import(command.TenantId, command.ProjectId, extracted, now);
        database.Add(record);
        var response = new ImportedFinancialRecordResponse(record.Id, record.ImportSource, record.ConfirmedAt, extracted.AmountMinor, extracted.Date, extracted.Category, extracted.Confidence, record.CreatedAt, (int)record.Version);
        database.IdempotencyRecords.Add(IdempotencyRecord.Create(command.TenantId, operation, command.IdempotencyKey, JsonSerializer.Serialize(response), now));
        await database.SaveChangesAsync(ct);
        return RecordResult<ImportedFinancialRecordResponse>.Success(response);
    }

    private async Task<RecordResult<FinancialRecordResponse>> ConfirmAsync<T>(ConfirmImportedFinancialRecordCommand command, string operation, string eventName, string aggregateType, string idName, CancellationToken ct) where T : FinancialRecord
    {
        var replay = await ReplayAsync<FinancialRecordResponse>(command.TenantId, operation, command.IdempotencyKey, ct);
        if (replay is not null) return RecordResult<FinancialRecordResponse>.Success(replay);
        var record = await database.Set<T>().SingleOrDefaultAsync(item => item.Id == command.RecordId && item.TenantId == command.TenantId, ct);
        if (record is null) return RecordResult<FinancialRecordResponse>.NotFound();
        var now = DateTimeOffset.UtcNow;
        if (!record.TryConfirm(command.AmountMinor, command.Date, command.Category?.Trim(), command.Recurring, command.RecurringInterval, command.UpdateRecurringInterval, command.ExpectedVersion, now)) return RecordResult<FinancialRecordResponse>.Conflict();
        var response = ToResponse(record);
        database.OutboxMessages.Add(OutboxMessage.Create(eventName, aggregateType, record.Id, record.Version, record.TenantId, now, JsonSerializer.Serialize(new Dictionary<string, object?> { [idName] = record.Id, ["project_id"] = record.ProjectId, ["amount_minor"] = record.AmountMinor, ["date"] = record.Date, ["category"] = record.Category, ["recurring"] = record.Recurring, ["recurring_interval"] = record.RecurringInterval, ["recorded_at"] = now })));
        database.IdempotencyRecords.Add(IdempotencyRecord.Create(command.TenantId, operation, command.IdempotencyKey, JsonSerializer.Serialize(response), now));
        await database.SaveChangesAsync(ct);
        return RecordResult<FinancialRecordResponse>.Success(response);
    }

    private async Task<RecordResult<DeletedFinancialRecordResponse>> DeleteAsync<T>(DeleteFinancialRecordCommand command, string operation, CancellationToken ct) where T : FinancialRecord
    {
        var replay = await ReplayAsync<DeletedFinancialRecordResponse>(command.TenantId, operation, command.IdempotencyKey, ct);
        if (replay is not null) return RecordResult<DeletedFinancialRecordResponse>.Success(replay);
        var record = await database.Set<T>().SingleOrDefaultAsync(item => item.Id == command.RecordId && item.TenantId == command.TenantId, ct);
        if (record is null) return RecordResult<DeletedFinancialRecordResponse>.NotFound();
        if (record.Version != command.ExpectedVersion) return RecordResult<DeletedFinancialRecordResponse>.Conflict();
        database.Remove(record);
        var response = new DeletedFinancialRecordResponse(record.Id);
        database.IdempotencyRecords.Add(IdempotencyRecord.Create(command.TenantId, operation, command.IdempotencyKey, JsonSerializer.Serialize(response), DateTimeOffset.UtcNow));
        await database.SaveChangesAsync(ct);
        return RecordResult<DeletedFinancialRecordResponse>.Success(response);
    }

    private async Task<RecordResult<FinancialRecordResponse>> ReconcileAsync<T>(ReconcileFinancialRecordCommand command, string operation, string dependentType, CancellationToken ct) where T : FinancialRecord
    {
        var replay = await ReplayAsync<FinancialRecordResponse>(command.TenantId, operation, command.IdempotencyKey, ct);
        if (replay is not null) return RecordResult<FinancialRecordResponse>.Success(replay);
        var record = await database.Set<T>().SingleOrDefaultAsync(item => item.Id == command.RecordId && item.TenantId == command.TenantId, ct);
        if (record is null) return RecordResult<FinancialRecordResponse>.NotFound();
        if (!await database.TransactionReplicas.AnyAsync(item => item.TransactionId == command.TransactionId && item.TenantId == command.TenantId && item.Status == "active", ct)) return RecordResult<FinancialRecordResponse>.TransactionUnavailable();
        if (!await claimValidator.IsValidAsync(command.ReconciliationClaimId, command.TransactionId, dependentType, record.Id, ct)) return RecordResult<FinancialRecordResponse>.ClaimUnavailable();
        if (!record.TryReconcile(command.TransactionId, command.ExpectedVersion, DateTimeOffset.UtcNow)) return RecordResult<FinancialRecordResponse>.Conflict();
        var response = ToResponse(record);
        database.IdempotencyRecords.Add(IdempotencyRecord.Create(command.TenantId, operation, command.IdempotencyKey, JsonSerializer.Serialize(response), record.UpdatedAt));
        await database.SaveChangesAsync(ct);
        return RecordResult<FinancialRecordResponse>.Success(response);
    }

    private async Task<T?> ReplayAsync<T>(Guid tenantId, string operation, string key, CancellationToken ct) where T : class => JsonSerializer.Deserialize<T>(await database.IdempotencyRecords.AsNoTracking().Where(item => item.TenantId == tenantId && item.Operation == operation && item.Key == key).Select(item => item.Result).SingleOrDefaultAsync(ct) ?? "null");
    public static FinancialRecordResponse ToResponse(FinancialRecord record) => new(record.Id, record.ProjectId, record.AmountMinor, record.Date, record.Category, record.Recurring, record.RecurringInterval, record.ImportSource, record.ConfirmedAt, record.ReconciledTransactionId, record.CreatedAt, record.UpdatedAt, (int)record.Version);
}

public sealed record CreateFinancialRecordCommand(Guid TenantId, string IdempotencyKey, Guid ProjectId, long AmountMinor, DateOnly Date, string Category, bool Recurring, string? RecurringInterval);
public sealed record UpdateFinancialRecordCommand(Guid TenantId, string IdempotencyKey, Guid RecordId, int ExpectedVersion, long? AmountMinor, DateOnly? Date, string? Category, bool? Recurring, string? RecurringInterval, bool UpdateRecurringInterval);
public sealed record DeleteFinancialRecordCommand(Guid TenantId, string IdempotencyKey, Guid RecordId, int ExpectedVersion);
public sealed record ReconcileFinancialRecordCommand(Guid TenantId, string IdempotencyKey, Guid RecordId, Guid TransactionId, Guid ReconciliationClaimId, int ExpectedVersion);
public sealed record ImportFinancialRecordCommand(Guid TenantId, string IdempotencyKey, Guid ProjectId, byte[] Document);
public sealed record ConfirmImportedFinancialRecordCommand(Guid TenantId, string IdempotencyKey, Guid RecordId, int ExpectedVersion, long? AmountMinor, DateOnly? Date, string? Category, bool? Recurring, string? RecurringInterval, bool UpdateRecurringInterval);
public sealed record FinancialRecordResponse(Guid Id, Guid ProjectId, long AmountMinor, DateOnly Date, string Category, bool Recurring, string? RecurringInterval, string ImportSource, DateTimeOffset? ConfirmedAt, Guid? ReconciledTransactionId, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, int Version);
public sealed record ImportedFinancialRecordResponse(Guid Id, string ImportSource, DateTimeOffset? ConfirmedAt, long? AmountMinor, DateOnly? Date, string? Category, decimal? Confidence, DateTimeOffset CreatedAt, int Version);
public sealed record DeletedFinancialRecordResponse(Guid Id);
public sealed class RecordResult<T> where T : class { public T? Value { get; private init; } public string? Error { get; private init; } public static RecordResult<T> Success(T value) => new() { Value = value }; public static RecordResult<T> NotFound() => new() { Error = "not-found" }; public static RecordResult<T> Conflict() => new() { Error = "conflict" }; public static RecordResult<T> ProjectUnavailable() => new() { Error = "project-unavailable" }; public static RecordResult<T> TransactionUnavailable() => new() { Error = "transaction-unavailable" }; public static RecordResult<T> ClaimUnavailable() => new() { Error = "claim-unavailable" }; public static RecordResult<T> ExtractionUnavailable() => new() { Error = "extraction-unavailable" }; }
