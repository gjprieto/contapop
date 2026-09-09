using System.Text.Json;
using Contapop.Billing.Service.Application.Abstractions;
using Contapop.Billing.Service.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Contapop.Billing.Service.Application.Commands;

public sealed class PaymentCommandHandler(BillingDbContext database, IReconciliationClaimValidator claimValidator)
{
    public async Task<PaymentCommandResult<PaymentResponse>> RecordAsync(RecordPaymentCommand command, CancellationToken cancellationToken)
    {
        var replay = await ReplayAsync<PaymentResponse>(command.TenantId, "record-payment", command.IdempotencyKey, cancellationToken);
        if (replay is not null) return PaymentCommandResult<PaymentResponse>.Success(replay);
        var invoice = await database.Invoices.SingleOrDefaultAsync(item => item.Id == command.InvoiceId && item.TenantId == command.TenantId, cancellationToken);
        if (invoice is null) return PaymentCommandResult<PaymentResponse>.NotFound();
        if (invoice.Status is "draft" or "void") return PaymentCommandResult<PaymentResponse>.Conflict();

        var now = DateTimeOffset.UtcNow;
        var payment = Payment.Create(command.TenantId, command.InvoiceId, command.AmountMinor, command.Date, command.PaymentMethod, now);
        database.Payments.Add(payment);
        database.OutboxMessages.Add(OutboxMessage.Create("billing.payment-recorded.v1", "Payment", payment.Id, payment.Version, payment.TenantId, now,
            JsonSerializer.Serialize(new { payment_id = payment.Id, invoice_id = invoice.Id, project_id = invoice.ProjectId, amount_minor = payment.AmountMinor, date = payment.Date, payment_method = payment.PaymentMethod, recorded_at = now })));

        var paidAmount = await database.Payments.Where(item => item.InvoiceId == invoice.Id).SumAsync(item => (long?)item.AmountMinor, cancellationToken) ?? 0;
        if (checked(paidAmount + payment.AmountMinor) >= invoice.TotalAmountMinor && invoice.TryMarkPaid(now))
        {
            database.OutboxMessages.Add(OutboxMessage.Create("billing.invoice-paid.v1", "Invoice", invoice.Id, invoice.Version, invoice.TenantId, now,
                JsonSerializer.Serialize(new { invoice_id = invoice.Id, project_id = invoice.ProjectId, direction = invoice.Direction, total_amount_minor = invoice.TotalAmountMinor, paid_at = now })));
        }

        var result = ToResponse(payment);
        database.IdempotencyRecords.Add(IdempotencyRecord.Create(command.TenantId, "record-payment", command.IdempotencyKey, JsonSerializer.Serialize(result), now));
        await database.SaveChangesAsync(cancellationToken);
        return PaymentCommandResult<PaymentResponse>.Success(result);
    }

    public async Task<PaymentCommandResult<PaymentResponse>> ReconcileAsync(ReconcilePaymentCommand command, CancellationToken cancellationToken)
    {
        var replay = await ReplayAsync<PaymentResponse>(command.TenantId, "reconcile-payment", command.IdempotencyKey, cancellationToken);
        if (replay is not null) return PaymentCommandResult<PaymentResponse>.Success(replay);
        var payment = await database.Payments.SingleOrDefaultAsync(item => item.Id == command.PaymentId && item.TenantId == command.TenantId, cancellationToken);
        if (payment is null) return PaymentCommandResult<PaymentResponse>.NotFound();
        if (!await database.TransactionReplicas.AnyAsync(item => item.TransactionId == command.TransactionId && item.TenantId == command.TenantId && item.Status == "active", cancellationToken)) return PaymentCommandResult<PaymentResponse>.TransactionUnavailable();
        if (!await claimValidator.IsValidAsync(command.ReconciliationClaimId, command.TransactionId, payment.Id, cancellationToken)) return PaymentCommandResult<PaymentResponse>.ClaimUnavailable();
        var now = DateTimeOffset.UtcNow;
        if (!payment.TryReconcile(command.TransactionId, command.ExpectedVersion, now)) return PaymentCommandResult<PaymentResponse>.Conflict();
        var result = ToResponse(payment);
        database.IdempotencyRecords.Add(IdempotencyRecord.Create(command.TenantId, "reconcile-payment", command.IdempotencyKey, JsonSerializer.Serialize(result), now));
        await database.SaveChangesAsync(cancellationToken);
        return PaymentCommandResult<PaymentResponse>.Success(result);
    }

    private async Task<T?> ReplayAsync<T>(Guid tenantId, string operation, string key, CancellationToken cancellationToken) where T : class =>
        JsonSerializer.Deserialize<T>(await database.IdempotencyRecords.AsNoTracking().Where(record => record.TenantId == tenantId && record.Operation == operation && record.Key == key).Select(record => record.Result).SingleOrDefaultAsync(cancellationToken) ?? "null");

    public static PaymentResponse ToResponse(Payment payment) => new(payment.Id, payment.InvoiceId, payment.AmountMinor, payment.Date, payment.PaymentMethod, payment.ReconciledTransactionId, payment.CreatedAt, payment.UpdatedAt, (int)payment.Version);
}

public sealed record PaymentResponse(Guid PaymentId, Guid InvoiceId, long AmountMinor, DateOnly Date, string PaymentMethod, Guid? ReconciledTransactionId, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, int Version);
public sealed class PaymentCommandResult<T> where T : class
{
    public T? Value { get; private init; }
    public string? Error { get; private init; }
    public static PaymentCommandResult<T> Success(T value) => new() { Value = value };
    public static PaymentCommandResult<T> NotFound() => new() { Error = "not-found" };
    public static PaymentCommandResult<T> Conflict() => new() { Error = "conflict" };
    public static PaymentCommandResult<T> TransactionUnavailable() => new() { Error = "transaction-unavailable" };
    public static PaymentCommandResult<T> ClaimUnavailable() => new() { Error = "claim-unavailable" };
}
