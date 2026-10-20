using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shared.Messaging.Abstractions.Events;
using Shared.Messaging.Abstractions.Processing;
using Shared.Messaging.Abstractions.Publishing;

namespace Shared.Messaging.Kafka;

public static class DependencyInjection
{
    public static IServiceCollection AddKafkaMessaging(
        this IServiceCollection services,
        IConfigurationSection configuration,
        Action<KafkaMessagingBuilder> configure)
    {
        services.AddOptions<KafkaOptions>()
            .Bind(configuration)
            .Validate(
                x => !string.IsNullOrWhiteSpace(x.BootstrapServers) && x.RetryDelaySeconds > 0,
                "Kafka bootstrap servers and positive retry delay are required.")
            .ValidateOnStart();
        services.TryAddSingleton<IEventTransport, KafkaTransport>();
        services.AddHostedService<KafkaTopicInitializer>();
        configure(new KafkaMessagingBuilder(services));

        return services;
    }
}

public sealed class KafkaMessagingBuilder(IServiceCollection services)
{
    public void Publish<T>(
        EventDefinition<T> definition,
        string? topic = null)
        where T : class
    {
        var destination = topic ?? definition.Topic;

        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        services.Configure<EventRouteOptions>(x => x.Topics[definition.EventType] = destination);
        services.AddSingleton(new KafkaTopic(destination));
    }

    public void Subscribe<T, THandler>(KafkaSubscription<T> subscription)
        where T : class
        where THandler : class, IEventHandler<T>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subscription.Topic);
        ArgumentException.ThrowIfNullOrWhiteSpace(subscription.GroupId);
        ArgumentException.ThrowIfNullOrWhiteSpace(subscription.HandlerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(subscription.DeadLetterTopic);

        if (subscription.HandlerId.Length > 128 || subscription.Topic == subscription.DeadLetterTopic)
        {
            throw new ArgumentException("Invalid handler identity or DLQ topic.");
        }

        if (services.Any(x => x.ServiceType == typeof(KafkaSubscription<T>)))
        {
            throw new InvalidOperationException($"A subscription for {typeof(T).Name} is already registered.");
        }

        services.AddSingleton(subscription);
        services.AddSingleton(new KafkaTopic(subscription.Topic));
        services.AddSingleton(new KafkaTopic(subscription.DeadLetterTopic));
        services.TryAddScoped<IEventHandler<T>, THandler>();
        services.AddScoped<KafkaMessageProcessor<T>>();
        services.AddHostedService<KafkaConsumer<T>>();
    }
}

public sealed record KafkaTopic(string Name);
