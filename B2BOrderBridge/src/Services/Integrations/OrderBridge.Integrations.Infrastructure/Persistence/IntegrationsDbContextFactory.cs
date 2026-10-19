using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace OrderBridge.Integrations.Infrastructure.Persistence;
public sealed class IntegrationsDbContextFactory : IDesignTimeDbContextFactory<IntegrationsDbContext>
{
    public IntegrationsDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<IntegrationsDbContext>()
        .UseNpgsql(Environment.GetEnvironmentVariable("ConnectionStrings__Integrations")
            ?? "Host=localhost;Port=5434;Database=integrations;Username=integrations;Password=integrations_dev").Options);
}
