namespace Shared.Messaging.Kafka;

public sealed class KafkaOptions
{
    public string BootstrapServers { get; set; } = "localhost:9092";

    public string ClientId { get; set; } = $"orderbridge-{Environment.MachineName}";

    public int RetryDelaySeconds { get; set; } = 5;
}
