using Contapop.Ledger.Service.Infrastructure.Outbox;

namespace Contapop.Ledger.Service.Application.Abstractions;

public interface IIntegrationEventPublisher
{
    Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken);
}
