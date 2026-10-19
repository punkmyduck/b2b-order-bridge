using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using OrderBridge.Integrations.Domain.Models;
using OrderBridge.Integrations.Infrastructure.Messaging;
using OrderBridge.Integrations.Application.Features.OrderIntegrations.Models;
using Shared.Application.Persistence;

namespace OrderBridge.Integrations.Infrastructure.Persistence;

public sealed class IntegrationsDbContext(DbContextOptions<IntegrationsDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<OrderIntegration> OrderIntegrations => Set<OrderIntegration>();
    public DbSet<IntegrationAttempt> IntegrationAttempts => Set<IntegrationAttempt>();
    public DbSet<IntegrationOrder> IntegrationOrders => Set<IntegrationOrder>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
        => builder.Properties<DateTimeOffset>().HaveConversion<UtcDateTimeOffsetConverter>();
    protected override void OnModelCreating(ModelBuilder builder)
        => builder.ApplyConfigurationsFromAssembly(typeof(IntegrationsDbContext).Assembly);
}

public sealed class UtcDateTimeOffsetConverter() : ValueConverter<DateTimeOffset, DateTimeOffset>(
    value => value.ToUniversalTime(), value => value);
