using Shared.Messaging.Abstractions.Events;

namespace Shared.Messaging.Abstractions.Processing;

/// <summary>The callback must contain only transactional local changes, never external side effects.</summary>
public interface IInboxProcessor
{
    Task ProcessAsync<TPayload>(
        string handlerId,
        IntegrationEvent<TPayload> message,
        Func<CancellationToken, Task> handle,
        CancellationToken cancellationToken)
        where TPayload : class;
}
