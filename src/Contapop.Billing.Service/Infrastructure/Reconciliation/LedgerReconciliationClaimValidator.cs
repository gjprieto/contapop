using System.Net.Http.Json;
using Contapop.Billing.Service.Application.Abstractions;
using Microsoft.AspNetCore.Http;

namespace Contapop.Billing.Service.Infrastructure.Reconciliation;

public sealed class LedgerReconciliationClaimValidator(HttpClient client, IHttpContextAccessor httpContextAccessor) : IReconciliationClaimValidator
{
    public async Task<bool> IsValidAsync(Guid claimId, Guid transactionId, Guid paymentId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/reconciliation-claims/{claimId}/validate")
        {
            Content = JsonContent.Create(new { transactionId, dependentType = "payment", dependentId = paymentId }),
        };
        var authorization = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authorization)) request.Headers.TryAddWithoutValidation("Authorization", authorization);
        using var response = await client.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode;
    }
}
