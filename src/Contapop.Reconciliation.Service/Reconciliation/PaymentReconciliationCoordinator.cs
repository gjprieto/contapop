using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Contapop.Reconciliation.Service.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Contapop.Reconciliation.Service.Reconciliation;

public sealed class PaymentReconciliationCoordinator(
    ReconciliationDbContext database,
    IHttpClientFactory clients,
    InternalJwtIssuer jwtIssuer,
    ILogger<PaymentReconciliationCoordinator> logger)
{
    public async Task<ReconciliationOperation> StartAsync(Guid tenantId, StartPaymentReconciliationRequest request, string idempotencyKey, CancellationToken cancellationToken)
    {
        var existing = await database.Operations.SingleOrDefaultAsync(operation => operation.TenantId == tenantId && operation.IdempotencyKey == idempotencyKey, cancellationToken);
        if (existing is not null) return existing;

        var operation = ReconciliationOperation.Create(tenantId, request.TransactionId, request.PaymentId, request.PaymentVersion, idempotencyKey, DateTimeOffset.UtcNow);
        database.Operations.Add(operation);
        await database.SaveChangesAsync(cancellationToken);
        return operation;
    }

    public async Task ExecuteAsync(Guid operationId, CancellationToken cancellationToken)
    {
        var operation = await database.Operations.SingleOrDefaultAsync(item => item.Id == operationId, cancellationToken);
        if (operation is null || operation.Status == "completed") return;

        try
        {
            if (operation.Status == "pending-reserve")
            {
                var reservation = await ReserveAsync(operation, cancellationToken);
                if (reservation is null) return;
                operation.Reserve(reservation.ClaimId, reservation.Version, DateTimeOffset.UtcNow);
                await database.SaveChangesAsync(cancellationToken);
            }

            if (operation.Status == "reserved")
            {
                var dependentResult = await ReconcilePaymentAsync(operation, cancellationToken);
                if (dependentResult == DependentResult.Succeeded)
                {
                    operation.DependentSucceeded(DateTimeOffset.UtcNow);
                    await database.SaveChangesAsync(cancellationToken);
                }
                else if (dependentResult == DependentResult.Failed)
                {
                    operation.DependentFailed("Billing rejected the payment reconciliation.", DateTimeOffset.UtcNow);
                    await database.SaveChangesAsync(cancellationToken);
                }
                else return;
            }

            if (operation.Status == "pending-confirm")
            {
                if (await ConfirmAsync(operation, cancellationToken))
                {
                    operation.Complete(DateTimeOffset.UtcNow);
                    await database.SaveChangesAsync(cancellationToken);
                }
            }
            else if (operation.Status == "pending-release")
            {
                if (await ReleaseAsync(operation, cancellationToken))
                {
                    operation.Complete(DateTimeOffset.UtcNow);
                    await database.SaveChangesAsync(cancellationToken);
                }
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(exception, "Reconciliation operation {OperationId} will be retried.", operation.Id);
            operation.Retry(exception.Message, DateTimeOffset.UtcNow);
            await database.SaveChangesAsync(CancellationToken.None);
        }
    }

    public Task<ReconciliationOperation?> GetAsync(Guid operationId, CancellationToken cancellationToken) =>
        database.Operations.AsNoTracking().SingleOrDefaultAsync(operation => operation.Id == operationId, cancellationToken);

    private async Task<ReservationResponse?> ReserveAsync(ReconciliationOperation operation, CancellationToken cancellationToken)
    {
        using var request = Request(HttpMethod.Post, $"/api/v1/transactions/{operation.TransactionId}/reconciliation-claims", operation,
            new { dependentType = operation.DependentType, dependentId = operation.DependentId });
        using var response = await clients.CreateClient("ledger-service").SendAsync(request, cancellationToken);
        if (response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity)
        {
            operation.Retry($"Ledger reservation was rejected ({(int)response.StatusCode}).", DateTimeOffset.UtcNow);
            await database.SaveChangesAsync(cancellationToken);
            return null;
        }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ReservationResponse>(cancellationToken) ?? throw new HttpRequestException("Ledger returned an invalid reservation.");
    }

    private async Task<DependentResult> ReconcilePaymentAsync(ReconciliationOperation operation, CancellationToken cancellationToken)
    {
        using var request = Request(HttpMethod.Post, $"/api/v1/payments/{operation.DependentId}/reconcile", operation,
            new { transactionId = operation.TransactionId, reconciliationClaimId = operation.ClaimId });
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{operation.ExpectedDependentVersion}\"");
        using var response = await clients.CreateClient("billing-service").SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode) return DependentResult.Succeeded;
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity) return DependentResult.Failed;
        response.EnsureSuccessStatusCode();
        return DependentResult.Unknown;
    }

    private async Task<bool> ConfirmAsync(ReconciliationOperation operation, CancellationToken cancellationToken)
    {
        using var request = Request(HttpMethod.Post, $"/api/v1/reconciliation-claims/{operation.ClaimId}/confirm", operation);
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{operation.ClaimVersion}\"");
        using var response = await clients.CreateClient("ledger-service").SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode) return true;
        await RetryAsync(operation, $"Ledger confirmation failed ({(int)response.StatusCode}).", cancellationToken);
        return false;
    }

    private async Task<bool> ReleaseAsync(ReconciliationOperation operation, CancellationToken cancellationToken)
    {
        using var request = Request(HttpMethod.Delete, $"/api/v1/reconciliation-claims/{operation.ClaimId}", operation);
        using var response = await clients.CreateClient("ledger-service").SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode) return true;
        await RetryAsync(operation, $"Ledger release failed ({(int)response.StatusCode}).", cancellationToken);
        return false;
    }

    private async Task RetryAsync(ReconciliationOperation operation, string error, CancellationToken cancellationToken)
    {
        operation.Retry(error, DateTimeOffset.UtcNow);
        await database.SaveChangesAsync(cancellationToken);
    }

    private HttpRequestMessage Request(HttpMethod method, string path, ReconciliationOperation operation, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwtIssuer.Create(operation.TenantId));
        request.Headers.TryAddWithoutValidation("Idempotency-Key", operation.Id.ToString());
        if (body is not null) request.Content = JsonContent.Create(body);
        return request;
    }

    private enum DependentResult { Succeeded, Failed, Unknown }
    private sealed record ReservationResponse(Guid ClaimId, int Version);
}

public sealed record StartPaymentReconciliationRequest(Guid TransactionId, Guid PaymentId, int PaymentVersion);

public sealed class InternalJwtIssuer(IConfiguration configuration)
{
    public string Create(Guid tenantId)
    {
        var key = configuration["InternalJwt:SigningKey"] ?? throw new InvalidOperationException("Internal JWT signing key is not configured.");
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(claims: [new Claim("tenant_id", tenantId.ToString()), new Claim(ClaimTypes.Role, "account-owner")], notBefore: DateTime.UtcNow, expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
