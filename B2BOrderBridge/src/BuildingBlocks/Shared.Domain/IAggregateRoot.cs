namespace Shared.Domain;

/// <summary>A consistency boundary with pending, in-process business facts.</summary>
public interface IAggregateRoot
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }
    void ClearDomainEvents();
}
