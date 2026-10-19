using Microsoft.Extensions.Configuration;

namespace OrderBridge.Integrations.Infrastructure.Messaging;

public sealed record KafkaOptions(string BootstrapServers, string OrderCreatedTopic, string DeadLetterTopic, string ConsumerGroup)
{
    public static KafkaOptions FromConfiguration(IConfiguration configuration)
    {
        var options = new KafkaOptions(
            configuration["Kafka:BootstrapServers"] ?? "localhost:9092",
            configuration["Kafka:OrderCreatedTopic"] ?? "orderbridge.orders.created.v1",
            configuration["Kafka:DeadLetterTopic"] ?? "orderbridge.orders.created.v1.dlq",
            configuration["Kafka:ConsumerGroup"] ?? "orderbridge-integration-planner-v1");
        ArgumentException.ThrowIfNullOrWhiteSpace(options.BootstrapServers);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.OrderCreatedTopic);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.DeadLetterTopic);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ConsumerGroup);
        if (options.OrderCreatedTopic == options.DeadLetterTopic)
            throw new InvalidOperationException("The event and dead-letter topics must be different.");
        return options;
    }
}
