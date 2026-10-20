namespace Shared.Messaging.Abstractions.Publishing;

public sealed record OutboxDeliveryOptions(DateTimeOffset? NotBefore = null);
