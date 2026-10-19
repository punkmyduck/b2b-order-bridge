using OrderBridge.Integrations.Application.Messaging;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderBridge.Contracts.Events;

namespace OrderBridge.Integrations.Infrastructure.Messaging;

public sealed class OrderCreatedConsumer(
    IServiceScopeFactory scopes, KafkaOptions options, IKafkaEventPublisher publisher,
    ILogger<OrderCreatedConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
            {
                BootstrapServers = options.BootstrapServers,
                GroupId = options.ConsumerGroup,
                EnableAutoCommit = false,
                EnableAutoOffsetStore = false,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                AllowAutoCreateTopics = false
            }).Build();
            try
            {
                consumer.Subscribe(options.OrderCreatedTopic);
                while (!stoppingToken.IsCancellationRequested)
                {
                    var record = consumer.Consume(stoppingToken);
                    OrderCreatedV1? message = null;
                    try
                    {
                        message = JsonSerializer.Deserialize<OrderCreatedV1>(record.Message.Value ?? "null", EventJson.Options)
                            ?? throw new JsonException("The event payload is null.");
                        OrderCreatedProcessor.ReadScenario(message);
                        if (!Guid.TryParse(record.Message.Key, out var key) || key != message.Order.Id)
                            throw new JsonException("The Kafka key must match the order ID.");
                    }
                    catch (JsonException ex)
                    {
                        // Poison records are retained for inspection. Do not commit if DLQ publication fails.
                        var deadLetter = JsonSerializer.Serialize(new
                        {
                            sourceTopic = record.Topic, partition = record.Partition.Value, offset = record.Offset.Value,
                            key = record.Message.Key, payload = record.Message.Value,
                            error = ex.Message, failedAt = DateTimeOffset.UtcNow
                        }, EventJson.Options);
                        await publisher.PublishAsync(options.DeadLetterTopic,
                            $"{record.Topic}:{record.Partition.Value}:{record.Offset.Value}", deadLetter, stoppingToken);
                        consumer.Commit(record);
                        logger.LogWarning("Invalid event at {Position} was sent to DLQ", record.TopicPartitionOffset);
                        continue;
                    }

                    await using var scope = scopes.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<OrderCreatedProcessor>().ProcessAsync(message, stoppingToken);
                    // The inbox and integrations have committed before the offset advances.
                    consumer.Commit(record);
                    logger.LogInformation("Processed OrderCreated {EventId} for order {OrderId}", message.EventId, message.Order.Id);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                // Rejoin from committed offsets instead of consuming past a failed record.
                logger.LogError(ex, "OrderCreated consumer failed; uncommitted records will be retried");
            }
            finally
            {
                try { consumer.Close(); }
                catch (KafkaException ex) { logger.LogWarning(ex, "Kafka consumer close failed"); }
            }
            try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
        }
    }
}
