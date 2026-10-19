using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Shared.Application.Behaviors;

namespace OrderBridge.Orders.Application;

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



        return services;
    }
}
