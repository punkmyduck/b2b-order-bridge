using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Shared.Messaging.Kafka;

// Demo provisioning. Production deployment should create topics separately.
public sealed class KafkaTopicInitializer(
    IEnumerable<KafkaTopic> topics,
    IOptions<KafkaOptions> options,
    ILogger<KafkaTopicInitializer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var admin = new AdminClientBuilder(
                    new AdminClientConfig
                    {
                        BootstrapServers = options.Value.BootstrapServers,
                        SocketTimeoutMs = 5000
                    }).Build();

                try
                {
                    await admin.CreateTopicsAsync(
                        topics.Select(x => x.Name).Distinct()
                            .Select(
                                topic => new TopicSpecification
                                {
                                    Name = topic,
                                    NumPartitions = 3,
                                    ReplicationFactor = 1,
                                    Configs = new Dictionary<string, string> { ["retention.ms"] = "604800000" }
                                }),
                        new CreateTopicsOptions { RequestTimeout = TimeSpan.FromSeconds(10) });
                }
                catch (CreateTopicsException ex) when (ex.Results.All(x => !x.Error.IsError || x.Error.Code == ErrorCode.TopicAlreadyExists))
                {
                }

                return;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Kafka topic provisioning failed; retrying");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(options.Value.RetryDelaySeconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }
}
