using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Shared.Messaging.Kafka;

public sealed class KafkaConsumer<T>(
    IServiceScopeFactory scopes,
    KafkaSubscription<T> subscription,
    IOptions<KafkaOptions> options,
    ILogger<KafkaConsumer<T>> logger) : BackgroundService
    where T : class
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var consumer = new ConsumerBuilder<string, string>(
                new ConsumerConfig
                {
                    BootstrapServers = options.Value.BootstrapServers,
                    GroupId = subscription.GroupId,
                    EnableAutoCommit = false,
                    EnableAutoOffsetStore = false,
                    AutoOffsetReset = AutoOffsetReset.Earliest,
                    AllowAutoCreateTopics = false
                }).Build();

            try
            {
                consumer.Subscribe(subscription.Topic);
                while (!stoppingToken.IsCancellationRequested)
                {
                    var record = consumer.Consume(stoppingToken);
                    await using var scope = scopes.CreateAsyncScope();

                    await scope.ServiceProvider.GetRequiredService<KafkaMessageProcessor<T>>().ProcessAsync(
                        new(
                            record.Topic,
                            record.Partition.Value,
                            record.Offset.Value,
                            record.Message.Key,
                            record.Message.Value),
                        stoppingToken);
                    // Never advance past an uncommitted DB transaction or a failed DLQ write.
                    consumer.Commit(record);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Consumer {HandlerId} failed; rejoining from committed offsets",
                    subscription.HandlerId);
            }
            finally
            {
                try
                {
                    consumer.Close();
                }
                catch (KafkaException ex)
                {
                    logger.LogWarning(ex, "Kafka consumer close failed");
                }
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
