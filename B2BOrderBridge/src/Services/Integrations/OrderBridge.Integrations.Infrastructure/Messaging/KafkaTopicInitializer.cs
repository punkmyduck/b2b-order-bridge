using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OrderBridge.Integrations.Infrastructure.Messaging;

// Local/demo provisioning. In production topics should be provisioned by deployment tooling.
public sealed class KafkaTopicInitializer(KafkaOptions options, ILogger<KafkaTopicInitializer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var admin = new AdminClientBuilder(new AdminClientConfig
                    { BootstrapServers = options.BootstrapServers, SocketTimeoutMs = 5000 }).Build();
                try
                {
                    await admin.CreateTopicsAsync(
                        new[] { options.OrderCreatedTopic, options.DeadLetterTopic }.Select(topic => new TopicSpecification
                        {
                            Name = topic, NumPartitions = 3, ReplicationFactor = 1,
                            Configs = new Dictionary<string, string> { ["retention.ms"] = "604800000" }
                        }), new CreateTopicsOptions { RequestTimeout = TimeSpan.FromSeconds(10) });
                }
                catch (CreateTopicsException ex) when (ex.Results.All(x =>
                           !x.Error.IsError || x.Error.Code == ErrorCode.TopicAlreadyExists)) { }
                logger.LogInformation("Kafka topics are ready");
                return;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Kafka topic provisioning failed; retrying in 5 seconds");
            }
            try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
        }
    }
}
