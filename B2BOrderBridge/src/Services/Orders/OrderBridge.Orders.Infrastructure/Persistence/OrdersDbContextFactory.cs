using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace OrderBridge.Orders.Infrastructure.Persistence;
public sealed class OrdersDbContextFactory : IDesignTimeDbContextFactory<OrdersDbContext>
{
    public OrdersDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<OrdersDbContext>()
        .UseNpgsql(Environment.GetEnvironmentVariable("ConnectionStrings__Orders")
            ?? "Host=localhost;Port=5433;Database=orders;Username=orders;Password=orders_dev").Options);
}
