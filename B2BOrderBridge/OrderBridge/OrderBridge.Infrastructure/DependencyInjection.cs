using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderBridge.Application.Features.Orders.Repositories;
using OrderBridge.Infrastructure.Persistence;
using OrderBridge.Infrastructure.Services;
using Shared.Application.Persistence;
using Shared.Application.Services;

namespace OrderBridge.Infrastructure;

public static class DependencyInjection
{
    private const string OrderBridgeConnectionString = "OrderBridge";

    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddSingleton<IDateTimeOffsetProvider, DateTimeOffsetProvider>();
        return services;
    }

    public static IServiceCollection AddPersistence(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        services.AddDbContext<OrderBridgeDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<OrderBridgeDbContext>());
        services.AddScoped(typeof(IRepository<,>), typeof(EfRepository<,>));
        services.AddScoped(typeof(IReadRepository<,>), typeof(EfRepository<,>));
        services.AddScoped<IOrderReadRepository, OrderReadRepository>();
        return services;
    }

    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString(OrderBridgeConnectionString)
            ?? throw new InvalidOperationException(
                $"Connection string '{OrderBridgeConnectionString}' is not configured.");

        return services.AddPersistence(connectionString);
    }

    public static async Task ApplyPersistenceMigrationsAsync(
        this IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrderBridgeDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);
    }
}
