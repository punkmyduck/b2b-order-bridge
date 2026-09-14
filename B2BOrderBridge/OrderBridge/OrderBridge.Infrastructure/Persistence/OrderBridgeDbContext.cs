using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using OrderBridge.Domain.Models;
using Shared.Application.Persistence;

namespace OrderBridge.Infrastructure.Persistence;

public sealed class OrderBridgeDbContext(DbContextOptions<OrderBridgeDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderIntegration> OrderIntegrations => Set<OrderIntegration>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
        => builder.Properties<DateTimeOffset>().HaveConversion<UtcDateTimeOffsetConverter>();

    protected override void OnModelCreating(ModelBuilder builder)
        => builder.ApplyConfigurationsFromAssembly(typeof(OrderBridgeDbContext).Assembly);
}

public sealed class UtcDateTimeOffsetConverter() : ValueConverter<DateTimeOffset, DateTimeOffset>(
    value => value.ToUniversalTime(), value => value);
