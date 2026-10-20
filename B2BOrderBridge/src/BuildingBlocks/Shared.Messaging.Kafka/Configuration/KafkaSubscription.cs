using Shared.Messaging.Abstractions.Events;
using System.Text.Json;

namespace Shared.Messaging.Kafka;

public sealed class KafkaSubscription<T>
    where T : class
{
    public required EventDefinition<T> Definition { get; init; }

    public required string Topic { get; init; }

    public required string GroupId { get; init; }

    public required string HandlerId { get; init; }

    public required string DeadLetterTopic { get; init; }

    public Func<JsonElement, IntegrationEvent<T>>? LegacyReader { get; init; }
}
