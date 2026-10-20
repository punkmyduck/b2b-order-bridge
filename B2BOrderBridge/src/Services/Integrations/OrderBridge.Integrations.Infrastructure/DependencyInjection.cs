using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderBridge.Integrations.Infrastructure.Persistence;
using OrderBridge.Integrations.Infrastructure.Persistence.Repositories;
using OrderBridge.Integrations.Infrastructure.Services;
using Shared.Messaging;
using Shared.Messaging.Kafka;
using Shared.Messaging.Persistence;
using OrderBridge.Contracts.Events;
using Shared.Application.Persistence;
using Shared.Application.Services;
using OrderBridge.Integrations.Application.Features.OrderIntegrations.Repositories;
using OrderBridge.Integrations.Application.Features.OrderIntegrations.Services.Interfaces;
using OrderBridge.Integrations.Application.Messaging;
using OrderBridge.Integrations.Infrastructure.BackgroundJobs;

namespace OrderBridge.Integrations.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddSingleton<IDateTimeOffsetProvider, DateTimeOffsetProvider>();
        services.AddDbContext<IntegrationsDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<IntegrationsDbContext>());
        services.AddScoped(typeof(IRepository<,>), typeof(EfRepository<,>));
        services.AddScoped(typeof(IReadRepository<,>), typeof(EfRepository<,>));
        services.AddScoped<IOrderIntegrationReadRepository, OrderIntegrationReadRepository>();
        services.AddScoped<IIntegrationAttemptRepository, IntegrationAttemptRepository>();
        services.AddScoped<IIntegrationOrderSnapshotRepository, IntegrationOrderSnapshotRepository>();
        services.AddScoped<IIntegrationPlanningStore, IntegrationPlanningStore>();
        services.AddScoped<IIntegrationJobStore, IntegrationJobStore>();
        services.AddEventInbox<IntegrationsDbContext>();

        return services;
    }

    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Integrations")
            ?? throw new InvalidOperationException("ConnectionStrings:Integrations is required.");

        return services.AddPersistence(connectionString);
    }

    public static IServiceCollection AddMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddKafkaMessaging(
            configuration.GetSection("Kafka"),
            kafka => kafka.Subscribe<OrderCreatedV1, OrderCreatedHandler>(
                new KafkaSubscription<OrderCreatedV1>
                {
                    Definition = OrderCreatedEvent.Definition,
                    Topic = configuration["Kafka:OrderCreatedTopic"] ?? OrderCreatedEvent.Definition.Topic,
                    GroupId = configuration["Kafka:ConsumerGroup"] ?? "orderbridge-integration-planner-v1",
                    HandlerId = "order-created-integration-planner-v1",
                    DeadLetterTopic = configuration["Kafka:DeadLetterTopic"] ?? "orderbridge.orders.created.v1.dlq",
                    LegacyReader = OrderCreatedEvent.ReadLegacy
                }));

        services.AddHostedService<IntegrationWorker>();

        return services;
    }

    public static async Task ApplyPersistenceMigrationsAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<IntegrationsDbContext>().Database.MigrateAsync(cancellationToken);
    }
}
