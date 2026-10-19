namespace OrderBridge.Integrations.Infrastructure.Persistence;
// Local projection owned by Integrations, populated exclusively from the event payload.
public sealed class IntegrationOrder
{
    public Guid Id { get; set; }
    public string Payload { get; set; } = "";
    public DateTimeOffset ReceivedAt { get; set; }
}
