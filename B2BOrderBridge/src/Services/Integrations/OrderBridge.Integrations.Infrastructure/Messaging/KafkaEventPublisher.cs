using Confluent.Kafka;

namespace OrderBridge.Integrations.Infrastructure.Messaging;

public interface IKafkaEventPublisher
{
    Task PublishAsync(string topic, string key, string payload, CancellationToken cancellationToken);
}

public sealed class KafkaEventPublisher(KafkaOptions options) : IKafkaEventPublisher, IDisposable
{
    private readonly IProducer<string, string> _producer = new ProducerBuilder<string, string>(new ProducerConfig
    {
        BootstrapServers = options.BootstrapServers,
        ClientId = $"orderbridge-{Environment.MachineName}",
        EnableIdempotence = true,
        Acks = Acks.All,
        MessageTimeoutMs = 10000,
        SocketTimeoutMs = 10000,
        AllowAutoCreateTopics = false
    }).Build();

    public async Task PublishAsync(string topic, string key, string payload, CancellationToken cancellationToken)
    {
        await _producer.ProduceAsync(topic, new Message<string, string> { Key = key, Value = payload }, cancellationToken);
    }

    public void Dispose() => _producer.Dispose();
}
