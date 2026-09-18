using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using OrderBridge.Application.Features.OrderIntegrations.Services.Implementations;
using OrderBridge.Application.Features.OrderIntegrations.Services.IntegrationHandlers;
using OrderBridge.Application.Features.OrderIntegrations.Services.Interfaces;
using Shared.Application.Behaviors;

namespace OrderBridge.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services,
        string? mediatRLicenseKey = null)
    {
        services.AddMediatR(configuration =>
        {
            configuration.Lifetime = ServiceLifetime.Scoped;
            configuration.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
            configuration.AddOpenBehavior(typeof(ValidationBehaviour<,>));
            configuration.LicenseKey = mediatRLicenseKey;
        });

        services.AddValidatorsFromAssembly(
            typeof(DependencyInjection).Assembly,
            includeInternalTypes: true,
            lifetime: ServiceLifetime.Transient);

        services.AddScoped<IOrderIntegrationPlanner, OrderIntegrationPlanner>();
        services.AddSingleton<IOrderIntegrationRetryPolicy, BaseOrderIntegrationRetryPolicy>();
        services.AddScoped<IOrderIntegrationResolver, OrderIntegrationResolver>();

        services.AddScoped<IOrderIntegrationHandler, TelegramNotificationIntegrationMockHandler>();

        return services;
    }
}
