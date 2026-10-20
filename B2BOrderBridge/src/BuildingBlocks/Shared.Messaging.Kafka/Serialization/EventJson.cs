using Shared.Messaging.Abstractions.Events;
using System.Text.Json;

namespace Shared.Messaging.Kafka;

public static class EventJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web);

    public static IntegrationEvent<T> Deserialize<T>(
        string payload,
        EventDefinition<T> definition,
        Func<JsonElement, IntegrationEvent<T>>? legacyReader = null)
        where T : class
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            IntegrationEvent<T> message;

// Legacy conversion is registered by the owner of the contract, never hard-coded in transport.
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                !document.RootElement.TryGetProperty("metadata", out _) &&
                legacyReader is not null)
            {
                message = legacyReader(document.RootElement);
            }
            else
            {
                message = document.RootElement.Deserialize<IntegrationEvent<T>>(Options) ?? throw new InvalidEventException("The event is null.");
            }

            EventValidation.Validate(message, definition);

            return message;
        }
        catch (JsonException ex)
        {
            throw new InvalidEventException("The event JSON cannot be read.", ex);
        }
    }
}
