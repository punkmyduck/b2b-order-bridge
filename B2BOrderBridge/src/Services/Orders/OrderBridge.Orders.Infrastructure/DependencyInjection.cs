using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderBridge.Orders.Infrastructure.Persistence;
using OrderBridge.Orders.Infrastructure.Persistence.Repositories;
using OrderBridge.Orders.Infrastructure.Services;
using OrderBridge.Orders.Infrastructure.Messaging;
using Shared.Application.Persistence;
using Shared.Application.Services;
using OrderBridge.Orders.Application.Features.Orders.Repositories;
using OrderBridge.Orders.Application.Messaging;

namespace OrderBridge.Orders.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        services.AddSingleton<IDateTimeOffsetProvider, DateTimeOffsetProvider>();
        services.AddDbContext<OrdersDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<OrdersDbContext>());
        services.AddScoped(typeof(IRepository<,>), typeof(EfRepository<,>));
        services.AddScoped(typeof(IReadRepository<,>), typeof(EfRepository<,>));
        services.AddScoped<IOrderReadRepository, OrderReadRepository>();
        services.AddScoped<IOrderCreatedOutbox, OrderCreatedOutbox>();
        return services;
    }
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
        => services.AddPersistence(configuration.GetConnectionString("Orders")
            ?? throw new InvalidOperationException("ConnectionStrings:Orders is required."));

    public static IServiceCollection AddMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(KafkaOptions.FromConfiguration(configuration));
        services.AddSingleton<IKafkaEventPublisher, KafkaEventPublisher>();
        services.AddHostedService<KafkaTopicInitializer>();
        services.AddScoped<OutboxBatchPublisher>();
        services.AddHostedService<OutboxDispatcher>();
        return services;
    }
    public static async Task ApplyPersistenceMigrationsAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<OrdersDbContext>().Database.MigrateAsync(cancellationToken);
    }
}
