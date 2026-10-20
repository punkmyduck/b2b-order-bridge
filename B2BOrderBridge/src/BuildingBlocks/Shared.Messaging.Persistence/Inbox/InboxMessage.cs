namespace Shared.Messaging.Persistence;

public sealed class InboxMessage
{
    public string Consumer { get; set; } = "";

    public Guid EventId { get; set; }

    public string Source { get; set; } = "";

    public string EventType { get; set; } = "";

    public string IdempotencyKey { get; set; } = "";

    public string OrderingKey { get; set; } = "";

// Historical receipts created before envelopes have no stored payload.
    public string? Payload { get; set; }

    public DateTimeOffset ProcessedAt { get; set; }
}
