using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderBridge.Integrations.Application;
using OrderBridge.Integrations.Infrastructure;
using OrderBridge.Integrations.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;
namespace OrderBridge.Integrations.Infrastructure.Tests;
public sealed class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17-alpine").Build();
    public ServiceProvider Services { get; private set; } = null!;
    public async Task InitializeAsync()
    {
        await _database.StartAsync();
        Services = new ServiceCollection().AddLogging().AddApplication().AddPersistence(_database.GetConnectionString())
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        await Services.ApplyPersistenceMigrationsAsync();
    }
    public async Task DisposeAsync() { if (Services is not null) await Services.DisposeAsync(); await _database.DisposeAsync(); }
}
