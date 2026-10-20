using Shared.Messaging.Abstractions.Events;

namespace Shared.Messaging.Abstractions.Processing;

public interface IEventHandler<TPayload>
    where TPayload : class
{
    Task HandleAsync(
        IntegrationEvent<TPayload> message,
        CancellationToken cancellationToken);
}
