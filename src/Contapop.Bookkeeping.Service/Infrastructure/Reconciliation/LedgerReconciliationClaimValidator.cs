using System.Net.Http.Json;
using Contapop.Bookkeeping.Service.Application.Abstractions;

namespace Contapop.Bookkeeping.Service.Infrastructure.Reconciliation;

public sealed class LedgerReconciliationClaimValidator(HttpClient client, IHttpContextAccessor httpContextAccessor) : IReconciliationClaimValidator
{
    public async Task<bool> IsValidAsync(Guid claimId, Guid transactionId, string dependentType, Guid dependentId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/reconciliation-claims/{claimId}/validate") { Content = JsonContent.Create(new { transactionId, dependentType, dependentId }) };
        var authorization = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authorization)) request.Headers.TryAddWithoutValidation("Authorization", authorization);
        using var response = await client.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode;
    }
}
