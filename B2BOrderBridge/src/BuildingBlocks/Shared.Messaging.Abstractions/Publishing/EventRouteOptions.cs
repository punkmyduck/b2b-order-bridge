using Shared.Messaging.Abstractions.Events;

namespace Shared.Messaging.Abstractions.Publishing;

public sealed class EventRouteOptions
{
    public Dictionary<string, string> Topics { get; } = new(StringComparer.Ordinal);

    public string Resolve<T>(EventDefinition<T> definition)
        where T : class => Topics.GetValueOrDefault(definition.EventType, definition.Topic);
}
