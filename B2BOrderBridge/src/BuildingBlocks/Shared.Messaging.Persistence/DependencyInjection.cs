using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shared.Messaging.Abstractions.Processing;
using Shared.Messaging.Abstractions.Publishing;

namespace Shared.Messaging.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddEventOutbox<TDbContext>(this IServiceCollection services)
        where TDbContext : DbContext
    {
        services.AddOptions<EventRouteOptions>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IEventOutbox, EfEventOutbox<TDbContext>>();

        return services;
    }

    public static IServiceCollection AddEventInbox<TDbContext>(this IServiceCollection services)
        where TDbContext : DbContext
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IInboxProcessor, EfInboxProcessor<TDbContext>>();

        return services;
    }

    public static IServiceCollection AddOutboxDispatcher<TDbContext>(this IServiceCollection services)
        where TDbContext : DbContext
    {
        services.AddScoped<OutboxBatchPublisher<TDbContext>>();
        services.AddHostedService<OutboxDispatcher<TDbContext>>();

        return services;
    }
}
