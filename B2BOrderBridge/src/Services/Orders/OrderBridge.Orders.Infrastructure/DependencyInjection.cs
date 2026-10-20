using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderBridge.Orders.Infrastructure.Persistence;
using OrderBridge.Orders.Infrastructure.Persistence.Repositories;
using OrderBridge.Orders.Infrastructure.Services;
using Shared.Messaging;
using Shared.Messaging.Kafka;
using Shared.Messaging.Persistence;
using OrderBridge.Contracts.Events;
using Shared.Application.Persistence;
using Shared.Application.Services;
using OrderBridge.Orders.Application.Features.Orders.Repositories;

namespace OrderBridge.Orders.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddSingleton<IDateTimeOffsetProvider, DateTimeOffsetProvider>();
        services.AddDbContext<OrdersDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IUnitOfWork, OrdersUnitOfWork>();
        services.AddScoped(typeof(IRepository<,>), typeof(EfRepository<,>));
        services.AddScoped(typeof(IReadRepository<,>), typeof(EfRepository<,>));
        services.AddScoped<IOrderReadRepository, OrderReadRepository>();
        services.AddEventOutbox<OrdersDbContext>();

        return services;
    }

    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Orders")
            ?? throw new InvalidOperationException("ConnectionStrings:Orders is required.");

        return services.AddPersistence(connectionString);
    }

    public static IServiceCollection AddMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddKafkaMessaging(
            configuration.GetSection("Kafka"),
            kafka => kafka.Publish(OrderCreatedEvent.Definition, configuration["Kafka:OrderCreatedTopic"]));
        services.AddOutboxDispatcher<OrdersDbContext>();

        return services;
    }

    public static async Task ApplyPersistenceMigrationsAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<OrdersDbContext>().Database.MigrateAsync(cancellationToken);
    }
}
