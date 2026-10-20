using Confluent.Kafka;
using Microsoft.Extensions.Options;
using Shared.Messaging.Abstractions.Publishing;

namespace Shared.Messaging.Kafka;

public sealed class KafkaTransport(IOptions<KafkaOptions> options) : IEventTransport, IDisposable
{
    private readonly IProducer<string, string> _producer = new ProducerBuilder<string, string>(
        new ProducerConfig
        {
            BootstrapServers = options.Value.BootstrapServers,
            ClientId = options.Value.ClientId,
            EnableIdempotence = true,
            Acks = Acks.All,
            MessageTimeoutMs = 10000,
            SocketTimeoutMs = 10000,
            AllowAutoCreateTopics = false
        }).Build();

    public async Task PublishAsync(
        string topic,
        string key,
        string payload,
        CancellationToken cancellationToken) => await _producer.ProduceAsync(
            topic,
            new Message<string, string>
            {
                Key = key,
                Value = payload
            },
            cancellationToken);

    public void Dispose() => _producer.Dispose();
}
