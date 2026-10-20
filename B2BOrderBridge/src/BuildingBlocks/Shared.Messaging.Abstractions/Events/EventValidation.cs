namespace Shared.Messaging.Abstractions.Events;

public static class EventValidation
{
    public static void Validate<T>(
        IntegrationEvent<T> message,
        EventDefinition<T> definition)
        where T : class
    {
        var metadata = message.Metadata;

        if (metadata is null ||
            metadata.EventId == Guid.Empty ||
            metadata.OccurredAt == default ||
            message.Payload is null ||
            metadata.EventType != definition.EventType ||
            metadata.SchemaVersion <= 0 ||
            metadata.SchemaVersion != definition.SchemaVersion ||
            !Text(metadata.Source, 128) ||
            !Text(metadata.EventType, 128) ||
            !Text(metadata.IdempotencyKey, 512) ||
            !Text(metadata.OrderingKey, 512) ||
            metadata.CorrelationId?.Length > 256 ||
            metadata.TraceParent?.Length > 256 ||
            metadata.CausationId == Guid.Empty)
        {
            throw new InvalidEventException("Invalid event metadata, payload, type or schema version.");
        }
    }

    private static bool Text(
        string? value,
        int length) => !string.IsNullOrWhiteSpace(value) && value.Length <= length && value == value.Trim();
}
