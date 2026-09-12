namespace Contapop.Bookkeeping.Service.Application.Abstractions;

public interface IReconciliationClaimValidator
{
    Task<bool> IsValidAsync(Guid claimId, Guid transactionId, string dependentType, Guid dependentId, CancellationToken cancellationToken);
}
