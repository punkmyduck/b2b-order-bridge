namespace Shared.Messaging.Persistence;

/// <summary>Mutable delivery state. Event metadata and payload are stored unchanged in Payload.</summary>
public sealed class OutboxMessage
{
    public Guid Id { get; set; }

    public string EventType { get; set; } = "";

    public string Topic { get; set; } = "";

    public string OrderingKey { get; set; } = "";

    public string IdempotencyKey { get; set; } = "";

    public string Payload { get; set; } = "";

    public DateTimeOffset OccurredAt { get; set; }

    public DateTimeOffset NextAttemptAt { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public int PublishAttempts { get; set; }

    public string? LastError { get; set; }
}
