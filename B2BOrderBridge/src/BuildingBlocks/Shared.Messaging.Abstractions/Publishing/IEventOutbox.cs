using Shared.Messaging.Abstractions.Events;

namespace Shared.Messaging.Abstractions.Publishing;

public interface IEventOutbox
{
    void Add<TPayload>(
        EventDefinition<TPayload> definition,
        IntegrationEvent<TPayload> message,
        OutboxDeliveryOptions? delivery = null)
        where TPayload : class;
}
