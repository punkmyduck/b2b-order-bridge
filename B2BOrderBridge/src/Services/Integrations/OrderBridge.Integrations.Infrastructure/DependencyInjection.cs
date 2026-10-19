using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderBridge.Integrations.Infrastructure.Persistence;
using OrderBridge.Integrations.Infrastructure.Persistence.Repositories;
using OrderBridge.Integrations.Infrastructure.Services;
using OrderBridge.Integrations.Infrastructure.Messaging;
using Shared.Application.Persistence;
using Shared.Application.Services;
using OrderBridge.Integrations.Application.Features.OrderIntegrations.Repositories;
using OrderBridge.Integrations.Application.Features.OrderIntegrations.Services.Interfaces;
using OrderBridge.Integrations.Application.Messaging;
using OrderBridge.Integrations.Infrastructure.BackgroundJobs;

namespace OrderBridge.Integrations.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, string connectionString)
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
        services.AddScoped<OrderCreatedProcessor>();
        return services;
    }
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
        => services.AddPersistence(configuration.GetConnectionString("Integrations")
            ?? throw new InvalidOperationException("ConnectionStrings:Integrations is required."));

    public static IServiceCollection AddMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(KafkaOptions.FromConfiguration(configuration));
        services.AddSingleton<IKafkaEventPublisher, KafkaEventPublisher>();
        services.AddHostedService<KafkaTopicInitializer>();
        services.AddHostedService<OrderCreatedConsumer>();
        services.AddHostedService<IntegrationWorker>();
        return services;
    }
    public static async Task ApplyPersistenceMigrationsAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IntegrationsDbContext>().Database.MigrateAsync(cancellationToken);
    }
}
