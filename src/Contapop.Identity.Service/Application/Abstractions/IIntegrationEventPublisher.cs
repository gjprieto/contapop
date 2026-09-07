using Contapop.Identity.Service.Infrastructure.Outbox;

namespace Contapop.Identity.Service.Application.Abstractions;

public interface IIntegrationEventPublisher
{
    Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken);
}
