using Contapop.Bookkeeping.Service.Application.Abstractions;

namespace Contapop.Bookkeeping.Service.Infrastructure.Persistence;

public abstract class FinancialRecord
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid? ReconciledTransactionId { get; private set; }
    public long AmountMinor { get; private set; }
    public DateOnly Date { get; private set; }
    public string Category { get; private set; } = null!;
    public bool Recurring { get; private set; }
    public string? RecurringInterval { get; private set; }
    public string ImportSource { get; private set; } = null!;
    public DateTimeOffset? ConfirmedAt { get; private set; }
    public long Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    protected static T Create<T>(Guid tenantId, Guid projectId, long amountMinor, DateOnly date, string category, bool recurring, string? recurringInterval, string importSource, DateTimeOffset? confirmedAt, DateTimeOffset now) where T : FinancialRecord, new() => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, ProjectId = projectId, AmountMinor = amountMinor, Date = date,
        Category = category, Recurring = recurring, RecurringInterval = recurringInterval, ImportSource = importSource,
        ConfirmedAt = confirmedAt, Version = 1, CreatedAt = now, UpdatedAt = now,
    };

    public bool TryUpdate(long? amountMinor, DateOnly? date, string? category, bool? recurring, string? recurringInterval, bool updateRecurringInterval, int expectedVersion, DateTimeOffset now)
    {
        if (Version != expectedVersion) return false;
        var actualRecurring = recurring ?? Recurring;
        var actualInterval = updateRecurringInterval ? recurringInterval : RecurringInterval;
        if (!Valid(actualRecurring, actualInterval)) return false;
        if (amountMinor.HasValue) AmountMinor = amountMinor.Value;
        if (date.HasValue) Date = date.Value;
        if (category is not null) Category = category;
        if (recurring.HasValue) Recurring = recurring.Value;
        if (updateRecurringInterval) RecurringInterval = recurringInterval;
        Version++; UpdatedAt = now;
        return true;
    }

    public bool TryReconcile(Guid transactionId, int expectedVersion, DateTimeOffset now)
    {
        if (Version != expectedVersion || (ReconciledTransactionId is not null && ReconciledTransactionId != transactionId)) return false;
        if (ReconciledTransactionId == transactionId) return true;
        ReconciledTransactionId = transactionId; Version++; UpdatedAt = now;
        return true;
    }

    public bool TryConfirm(long? amountMinor, DateOnly? date, string? category, bool? recurring, string? recurringInterval, bool updateRecurringInterval, int expectedVersion, DateTimeOffset now)
    {
        if (ImportSource != "pdf_ocr" || ConfirmedAt is not null || Version != expectedVersion) return false;
        var actualAmount = amountMinor ?? AmountMinor;
        var actualDate = date ?? Date;
        var actualCategory = category ?? Category;
        var actualRecurring = recurring ?? Recurring;
        var actualInterval = updateRecurringInterval ? recurringInterval : RecurringInterval;
        if (actualAmount <= 0 || actualDate == default || string.IsNullOrWhiteSpace(actualCategory) || !Valid(actualRecurring, actualInterval)) return false;
        AmountMinor = actualAmount; Date = actualDate; Category = actualCategory; Recurring = actualRecurring; RecurringInterval = actualInterval;
        ConfirmedAt = now; Version++; UpdatedAt = now;
        return true;
    }

    public static bool Valid(bool recurring, string? interval) => recurring ? interval is "weekly" or "monthly" or "yearly" : interval is null;
}

public sealed class Expense : FinancialRecord
{
    public static Expense Create(Guid tenantId, Guid projectId, long amountMinor, DateOnly date, string category, bool recurring, string? recurringInterval, DateTimeOffset now) => Create<Expense>(tenantId, projectId, amountMinor, date, category, recurring, recurringInterval, "manual", now, now);
    public static Expense Import(Guid tenantId, Guid projectId, DocumentExtractionResult extracted, DateTimeOffset now) => Create<Expense>(tenantId, projectId, extracted.AmountMinor ?? 0, extracted.Date ?? default, extracted.Category ?? string.Empty, false, null, "pdf_ocr", null, now);
}

public sealed class Revenue : FinancialRecord
{
    public static Revenue Create(Guid tenantId, Guid projectId, long amountMinor, DateOnly date, string category, bool recurring, string? recurringInterval, DateTimeOffset now) => Create<Revenue>(tenantId, projectId, amountMinor, date, category, recurring, recurringInterval, "manual", now, now);
    public static Revenue Import(Guid tenantId, Guid projectId, DocumentExtractionResult extracted, DateTimeOffset now) => Create<Revenue>(tenantId, projectId, extracted.AmountMinor ?? 0, extracted.Date ?? default, extracted.Category ?? string.Empty, false, null, "pdf_ocr", null, now);
}

public sealed class IdempotencyRecord
{
    public Guid TenantId { get; private set; }
    public string Operation { get; private set; } = null!;
    public string Key { get; private set; } = null!;
    public string Result { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public static IdempotencyRecord Create(Guid tenantId, string operation, string key, string result, DateTimeOffset createdAt) => new() { TenantId = tenantId, Operation = operation, Key = key, Result = result, CreatedAt = createdAt };
}

public sealed class OutboxMessage
{
    public Guid EventId { get; private set; }
    public string EventName { get; private set; } = null!;
    public string AggregateType { get; private set; } = null!;
    public Guid AggregateId { get; private set; }
    public long AggregateVersion { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid? CorrelationId { get; private set; }
    public Guid? CausationId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public string Payload { get; private set; } = null!;
    public string Status { get; private set; } = null!;
    public static OutboxMessage Create(string eventName, string aggregateType, Guid aggregateId, long aggregateVersion, Guid tenantId, DateTimeOffset occurredAt, string payload) => new() { EventId = Guid.NewGuid(), EventName = eventName, AggregateType = aggregateType, AggregateId = aggregateId, AggregateVersion = aggregateVersion, TenantId = tenantId, OccurredAt = occurredAt, Payload = payload, Status = "pending" };
}
