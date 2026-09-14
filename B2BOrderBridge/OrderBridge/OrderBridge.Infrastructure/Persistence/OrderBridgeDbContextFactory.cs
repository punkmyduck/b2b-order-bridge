using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OrderBridge.Infrastructure.Persistence;

public sealed class OrderBridgeDbContextFactory : IDesignTimeDbContextFactory<OrderBridgeDbContext>
{
    public OrderBridgeDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__OrderBridge")
            ?? "Host=localhost;Database=orderbridge;Username=postgres";
        return new(new DbContextOptionsBuilder<OrderBridgeDbContext>().UseNpgsql(connectionString).Options);
    }
}
