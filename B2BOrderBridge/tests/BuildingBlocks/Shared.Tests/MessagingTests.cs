using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using OrderBridge.Contracts.Events;
using Shared.Messaging.Abstractions.Events;
using Shared.Messaging.Abstractions.Processing;
using Shared.Messaging.Abstractions.Publishing;
using Shared.Messaging.Kafka;
using Xunit;

namespace Shared.Tests;

public sealed class MessagingTests
{
    private static readonly EventDefinition<ExamplePayload> Definition = new(
        "example.created.v1",
        1,
        "examples");

    private static IntegrationEvent<ExamplePayload> Event() => IntegrationEvent<ExamplePayload>.Create(
        Definition,
        new("value"),
        "tests",
        "order-421",
        DateTimeOffset.UtcNow,
        "example-421");

    [Fact]
    public void Envelope_round_trips_metadata_and_payload_separately()
    {
        using var activity = new Activity("producer").SetIdFormat(ActivityIdFormat.W3C)
            .Start();
        var message = Event();
        var json = JsonSerializer.Serialize(message, EventJson.Options);
        var read = EventJson.Deserialize(json, Definition);

        Assert.Equal(message, read);
        Assert.Equal(activity.Id, read.Metadata.TraceParent);
        Assert.Equal("example-421", read.Metadata.IdempotencyKey);
        Assert.DoesNotContain("eventId", JsonDocument.Parse(json).RootElement.GetProperty("payload").ToString());
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{")]
    [InlineData("{}")]
    public void Invalid_JSON_or_missing_required_envelope_is_rejected(string json) => Assert.Throws<InvalidEventException>(() => EventJson.Deserialize(json, Definition));

    [Theory]
    [InlineData("version")]
    [InlineData("type")]
    [InlineData("ordering")]
    [InlineData("idempotency")]
    [InlineData("identity")]
    public void Invalid_metadata_is_rejected(string field)
    {
        var message = Event();
        var m = message.Metadata;

        m = field switch
        {
            "version" => m with
            {
                SchemaVersion = 2
            },
            "type" => m with
            {
                EventType = "unknown"
            },
            "ordering" => m with
            {
                OrderingKey = " "
            },
            "idempotency" => m with
            {
                IdempotencyKey = ""
            },
            _ => m with
            {
                EventId = Guid.Empty
            }
        };
        Assert.Throws<InvalidEventException>(() => EventValidation.Validate(message with { Metadata = m }, Definition));
    }

    [Fact]
    public void Legacy_contract_keeps_its_event_identity_when_normalized()
    {
        var id = Guid.NewGuid();
        var order = new OrderSnapshotV1(
            Guid.NewGuid(),
            "portal",
            "421",
            new(
                "Company",
                "123",
                "a@b.com"),
            "RUB",
            10,
            [new(
                Guid.NewGuid(),
                "SKU",
                "Item",
                1,
                10,
                10)]);
        var json = JsonSerializer.Serialize(
            new { eventId = id, occurredAt = DateTimeOffset.UtcNow, processingScenario = "FullCycle", order },
            EventJson.Options);
        var message = EventJson.Deserialize(
            json,
            OrderCreatedEvent.Definition,
            OrderCreatedEvent.ReadLegacy);

        Assert.Equal(id, message.Metadata.EventId);
        Assert.Equal(id.ToString("N"), message.Metadata.IdempotencyKey);
        Assert.Equal(order.Id.ToString("N"), message.Metadata.OrderingKey);
        Assert.Equal("FullCycle", message.Payload.ProcessingScenario);
    }

    [Fact]
    public async Task Handler_is_invoked_through_inbox_and_completion_is_awaited()
    {
        var message = Event();
        var handler = new Handler();
        var inbox = new Inbox();
        var transport = new Transport();

        await Processor(
            handler,
            inbox,
            transport).ProcessAsync(Record(message), default);
        Assert.Equal(message, handler.Received);
        Assert.True(inbox.Committed);
        Assert.Empty(transport.Messages);
    }

    [Theory]
    [InlineData("bad-json")]
    [InlineData("wrong-key")]
    [InlineData("permanent-handler-error")]
    public async Task Permanent_input_error_goes_to_DLQ(string reason)
    {
        var handler = new Handler

        {
            Error = reason == "permanent-handler-error" ? new InvalidEventException("bad input") : null
        };
        var transport = new Transport();
        var inbox = new Inbox();
        var record = Record(Event());

        record = reason switch
        {
            "bad-json" => record with
            {
                Value = "{"
            },
            "wrong-key" => record with
            {
                Key = "other"
            },
            _ => record
        };
        await Processor(
            handler,
            inbox,
            transport).ProcessAsync(record, default);
        var sent = Assert.Single(transport.Messages);

        Assert.Equal("examples.dlq", sent.Topic);
        Assert.Equal("examples:1:42", sent.Key);
        var dlq = JsonSerializer.Deserialize<DeadLetterMessage>(sent.Payload, EventJson.Options)!;

        Assert.Equal(record.Value, dlq.Payload);
        Assert.False(inbox.Committed);
    }

    [Fact]
    public async Task Database_failure_is_propagated_without_DLQ_so_offset_cannot_be_committed()
    {
        var transport = new Transport();
        var processor = Processor(
            new Handler { Error = new IOException("DB down") },
            new Inbox(),
            transport);

        await Assert.ThrowsAsync<IOException>(() => processor.ProcessAsync(Record(Event()), default));
        Assert.Empty(transport.Messages);
    }

    [Fact]
    public async Task Failed_DLQ_write_is_propagated_so_original_offset_cannot_be_committed()
    {
        var processor = Processor(
            new Handler(),
            new Inbox(),
            new Transport { Fail = true });

        await Assert.ThrowsAsync<IOException>(() => processor.ProcessAsync(Record(Event())with { Value = "{" }, default));
    }

    private static KafkaMessageProcessor<ExamplePayload> Processor(
        Handler handler,
        Inbox inbox,
        Transport transport) => new(
            new()
            {
                Definition = Definition,
                Topic = Definition.Topic,
                GroupId = "test-group",
                HandlerId = "test-handler",
                DeadLetterTopic = "examples.dlq"
            },
            handler,
            inbox,
            transport,
            NullLogger<KafkaMessageProcessor<ExamplePayload>>.Instance);

    private static KafkaRecord Record(IntegrationEvent<ExamplePayload> message) => new(
        "examples",
        1,
        42,
        message.Metadata.OrderingKey,
        JsonSerializer.Serialize(message, EventJson.Options));

    public sealed record ExamplePayload(string Name);

    private sealed class Handler : IEventHandler<ExamplePayload>
    {
        public Exception? Error { get; init; }

        public IntegrationEvent<ExamplePayload>? Received { get; private set; }

        public Task HandleAsync(
            IntegrationEvent<ExamplePayload> message,
            CancellationToken cancellationToken)
        {
            if (Error is not null)
            {
                throw Error;
            }

            Received = message;

            return Task.CompletedTask;
        }
    }

    private sealed class Inbox : IInboxProcessor
    {
        public bool Committed { get; private set; }

        public async Task ProcessAsync<T>(
            string handlerId,
            IntegrationEvent<T> message,
            Func<CancellationToken, Task> handle,
            CancellationToken cancellationToken)
            where T : class
        {
            await handle(cancellationToken);
            Committed = true;
        }
    }

    private sealed class Transport : IEventTransport
    {
        public bool Fail { get; init; }

        public List<(string Topic, string Key, string Payload)> Messages { get; } = [];

        public Task PublishAsync(
            string topic,
            string key,
            string payload,
            CancellationToken cancellationToken)
        {
            if (Fail)
            {
                throw new IOException("Kafka down");
            }

            Messages.Add((topic, key, payload));

            return Task.CompletedTask;
        }
    }
}
