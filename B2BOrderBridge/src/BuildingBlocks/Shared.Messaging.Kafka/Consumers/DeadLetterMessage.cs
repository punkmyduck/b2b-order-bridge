namespace Shared.Messaging.Kafka;

public sealed record DeadLetterMessage(
    string SourceTopic,
    int Partition,
    long Offset,
    string? Key,
    string? Payload,
    string Error,
    DateTimeOffset FailedAt);
