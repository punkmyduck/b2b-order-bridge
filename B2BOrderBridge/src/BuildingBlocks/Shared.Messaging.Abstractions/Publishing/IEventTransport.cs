namespace Shared.Messaging.Abstractions.Publishing;

/// <summary>Infrastructure transport, also used by the outbox dispatcher and DLQ writer.</summary>
public interface IEventTransport
{
    Task PublishAsync(
        string topic,
        string key,
        string payload,
        CancellationToken cancellationToken);
}
