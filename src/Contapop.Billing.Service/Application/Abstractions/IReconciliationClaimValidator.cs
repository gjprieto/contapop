namespace Contapop.Billing.Service.Application.Abstractions;

public interface IReconciliationClaimValidator
{
    Task<bool> IsValidAsync(Guid claimId, Guid transactionId, Guid paymentId, CancellationToken cancellationToken);
}
