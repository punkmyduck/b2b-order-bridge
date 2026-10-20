using MediatR;
using Shared.Domain;

namespace Shared.Application.Events;

/// <summary>In-process adapter. The domain itself does not depend on MediatR or transport contracts.</summary>
public sealed record DomainEventNotification<TEvent>(TEvent Event) : INotification
    where TEvent : IDomainEvent;

public interface IDomainEventDispatcher
{
    Task DispatchAsync(
        IDomainEvent domainEvent,
        CancellationToken cancellationToken);
}

public sealed class DomainEventDispatcher(IPublisher publisher) : IDomainEventDispatcher
{
    public Task DispatchAsync(
        IDomainEvent domainEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        cancellationToken.ThrowIfCancellationRequested();
        var notificationType = typeof(DomainEventNotification<>).MakeGenericType(domainEvent.GetType());
        var notification = (INotification)Activator.CreateInstance(notificationType, domainEvent)!;

        return publisher.Publish(notification, cancellationToken);
    }
}
