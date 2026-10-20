namespace Shared.Messaging.Kafka;

public sealed record KafkaRecord(
    string Topic,
    int Partition,
    long Offset,
    string? Key,
    string? Value);
