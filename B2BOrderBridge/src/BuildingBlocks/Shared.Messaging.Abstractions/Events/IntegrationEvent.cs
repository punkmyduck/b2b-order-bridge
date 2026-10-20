using System.Diagnostics;

namespace Shared.Messaging.Abstractions.Events;

public abstract record IntegrationEvent
{
    public required EventMetadata Metadata { get; init; }
}

/// <summary>The concrete contract is only TPayload; all shared metadata lives in the envelope.</summary>
public sealed record IntegrationEvent<TPayload> : IntegrationEvent
    where TPayload : class
{
    public required TPayload Payload { get; init; }

    public static IntegrationEvent<TPayload> Create(
        EventDefinition<TPayload> definition,
        TPayload payload,
        string source,
        string orderingKey,
        DateTimeOffset occurredAt,
        string? idempotencyKey = null,
        Guid? eventId = null,
        string? correlationId = null,
        Guid? causationId = null)
    {
        var id = eventId ?? Guid.NewGuid();

        var metadata = new EventMetadata(
            id,
            definition.EventType,
            definition.SchemaVersion,
            source,
            occurredAt,
            idempotencyKey ?? id.ToString("N"),
            orderingKey,
            correlationId ?? id.ToString("N"),
            causationId,
            Activity.Current?.Id);

        var message = new IntegrationEvent<TPayload>

        {
            Metadata = metadata,
            Payload = payload
        };

        EventValidation.Validate(message, definition);

        return message;
    }
}
