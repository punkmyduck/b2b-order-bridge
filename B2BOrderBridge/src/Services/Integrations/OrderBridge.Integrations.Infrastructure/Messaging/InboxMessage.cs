namespace OrderBridge.Integrations.Infrastructure.Messaging;
public sealed class InboxMessage
{
    public string Consumer { get; set; } = "";
    public Guid EventId { get; set; }
    public DateTimeOffset ProcessedAt { get; set; }
}
