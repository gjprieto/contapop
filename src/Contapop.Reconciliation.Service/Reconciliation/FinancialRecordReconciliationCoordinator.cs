using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Contapop.Reconciliation.Service.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Contapop.Reconciliation.Service.Reconciliation;

public sealed class FinancialRecordReconciliationCoordinator(
    ReconciliationDbContext database,
    IHttpClientFactory clients,
    InternalJwtIssuer jwtIssuer,
    ILogger<FinancialRecordReconciliationCoordinator> logger)
{
    public async Task<ReconciliationOperation> StartAsync(Guid tenantId, StartFinancialRecordReconciliationRequest request, string idempotencyKey, CancellationToken ct)
    {
        var existing = await database.Operations.SingleOrDefaultAsync(operation => operation.TenantId == tenantId && operation.IdempotencyKey == idempotencyKey, ct);
        if (existing is not null) return existing;
        var operation = ReconciliationOperation.Create(tenantId, request.TransactionId, request.RecordType, request.RecordId, request.RecordVersion, idempotencyKey, DateTimeOffset.UtcNow);
        database.Operations.Add(operation);
        await database.SaveChangesAsync(ct);
        return operation;
    }

    public async Task ExecuteAsync(Guid operationId, CancellationToken ct)
    {
        var operation = await database.Operations.SingleOrDefaultAsync(item => item.Id == operationId, ct);
        if (operation is null || operation.Status == "completed" || operation.DependentType == "payment") return;
        try
        {
            if (operation.Status == "pending-reserve")
            {
                using var request = Request(HttpMethod.Post, $"/api/v1/transactions/{operation.TransactionId}/reconciliation-claims", operation, new { dependentType = operation.DependentType, dependentId = operation.DependentId });
                using var response = await clients.CreateClient("ledger-service").SendAsync(request, ct);
                if (response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity) { await RetryAsync(operation, $"Ledger reservation was rejected ({(int)response.StatusCode}).", ct); return; }
                response.EnsureSuccessStatusCode();
                var reservation = await response.Content.ReadFromJsonAsync<ReservationResponse>(ct) ?? throw new HttpRequestException("Ledger returned an invalid reservation.");
                operation.Reserve(reservation.ClaimId, reservation.Version, DateTimeOffset.UtcNow);
                await database.SaveChangesAsync(ct);
            }
            if (operation.Status == "reserved")
            {
                using var request = Request(HttpMethod.Post, $"/api/v1/{operation.DependentType}s/{operation.DependentId}/reconcile", operation, new { transactionId = operation.TransactionId, reconciliationClaimId = operation.ClaimId });
                request.Headers.TryAddWithoutValidation("If-Match", $"\"{operation.ExpectedDependentVersion}\"");
                using var response = await clients.CreateClient("bookkeeping-service").SendAsync(request, ct);
                if (response.IsSuccessStatusCode) { operation.DependentSucceeded(DateTimeOffset.UtcNow); await database.SaveChangesAsync(ct); }
                else if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity) { operation.DependentFailed("Bookkeeping rejected the financial record reconciliation.", DateTimeOffset.UtcNow); await database.SaveChangesAsync(ct); }
                else return;
            }
            if (operation.Status == "pending-confirm")
            {
                using var request = Request(HttpMethod.Post, $"/api/v1/reconciliation-claims/{operation.ClaimId}/confirm", operation);
                request.Headers.TryAddWithoutValidation("If-Match", $"\"{operation.ClaimVersion}\"");
                using var response = await clients.CreateClient("ledger-service").SendAsync(request, ct);
                if (response.IsSuccessStatusCode) { operation.Complete(DateTimeOffset.UtcNow); await database.SaveChangesAsync(ct); }
                else await RetryAsync(operation, $"Ledger confirmation failed ({(int)response.StatusCode}).", ct);
            }
            else if (operation.Status == "pending-release")
            {
                using var request = Request(HttpMethod.Delete, $"/api/v1/reconciliation-claims/{operation.ClaimId}", operation);
                using var response = await clients.CreateClient("ledger-service").SendAsync(request, ct);
                if (response.IsSuccessStatusCode) { operation.Complete(DateTimeOffset.UtcNow); await database.SaveChangesAsync(ct); }
                else await RetryAsync(operation, $"Ledger release failed ({(int)response.StatusCode}).", ct);
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(exception, "Reconciliation operation {OperationId} will be retried.", operation.Id);
            operation.Retry(exception.Message, DateTimeOffset.UtcNow);
            await database.SaveChangesAsync(CancellationToken.None);
        }
    }

    private async Task RetryAsync(ReconciliationOperation operation, string error, CancellationToken ct) { operation.Retry(error, DateTimeOffset.UtcNow); await database.SaveChangesAsync(ct); }
    private HttpRequestMessage Request(HttpMethod method, string path, ReconciliationOperation operation, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwtIssuer.Create(operation.TenantId));
        request.Headers.TryAddWithoutValidation("Idempotency-Key", operation.Id.ToString());
        if (body is not null) request.Content = JsonContent.Create(body);
        return request;
    }
    private sealed record ReservationResponse(Guid ClaimId, int Version);
}

public sealed record StartFinancialRecordReconciliationRequest(Guid TransactionId, Guid RecordId, int RecordVersion, string RecordType);
