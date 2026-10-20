using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Shared.Messaging.Abstractions.Events;
using Shared.Messaging.Abstractions.Processing;
using Shared.Messaging.Abstractions.Publishing;

namespace Shared.Messaging.Kafka;

/// <summary>One record per scope. Returns only after the inbox transaction or DLQ publication succeeds.</summary>
public sealed class KafkaMessageProcessor<T>(
    KafkaSubscription<T> subscription,
    IEventHandler<T> handler,
    IInboxProcessor inbox,
    IEventTransport transport,
    ILogger<KafkaMessageProcessor<T>> logger)
    where T : class
{
    public async Task ProcessAsync(
        KafkaRecord record,
        CancellationToken cancellationToken)
    {
        try
        {
            var message = EventJson.Deserialize(
                record.Value ?? "null",
                subscription.Definition,
                subscription.LegacyReader);

            if (record.Key != message.Metadata.OrderingKey)
            {
                throw new InvalidEventException("The Kafka key must equal the event ordering key.");
            }

            using var activity = new Activity("Messaging.Consume").SetIdFormat(ActivityIdFormat.W3C);

            if (ActivityContext.TryParse(
                message.Metadata.TraceParent,
                null,
                out _))
            {
                activity.SetParentId(message.Metadata.TraceParent!);
            }

            activity.Start();
            activity.SetTag("messaging.message.id", message.Metadata.EventId);
            activity.SetTag("messaging.destination.name", record.Topic);

            await inbox.ProcessAsync(
                subscription.HandlerId,
                message,
                ct => handler.HandleAsync(message, ct),
                cancellationToken);
        }
        catch (InvalidEventException ex)
        {
            var deadLetter = new DeadLetterMessage(
                record.Topic,
                record.Partition,
                record.Offset,
                record.Key,
                record.Value,
                ex.Message,
                DateTimeOffset.UtcNow);

            await transport.PublishAsync(
                subscription.DeadLetterTopic,
                $"{record.Topic}:{record.Partition}:{record.Offset}",
                JsonSerializer.Serialize(deadLetter, EventJson.Options),
                cancellationToken);

            logger.LogWarning(
                ex,
                "Event {Topic}/{Partition}/{Offset} was sent to DLQ",
                record.Topic,
                record.Partition,
                record.Offset);
        }
    }
}
