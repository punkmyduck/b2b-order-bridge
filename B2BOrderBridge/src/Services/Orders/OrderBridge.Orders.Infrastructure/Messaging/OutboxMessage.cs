namespace OrderBridge.Orders.Infrastructure.Messaging;
public sealed class OutboxMessage
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public string EventType { get; set; } = "";
    public string Payload { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public int PublishAttempts { get; set; }
    public string? LastError { get; set; }
}
