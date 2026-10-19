using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Application.Persistence;
using Shared.Application.Pagination;
using Shared.Application.Services;
using Shared.Application.Results;
using Xunit;
using OrderBridge.Integrations.Domain.Models;
using OrderBridge.Integrations.Domain.ValueObjects;
using OrderBridge.Integrations.Application.Features.OrderIntegrations.Models;
using OrderBridge.Integrations.Application.Features.OrderIntegrations.Repositories;
using OrderBridge.Integrations.Application.Features.OrderIntegrations.ValueObjects;
using OrderBridge.Integrations.Application.ValueObjects;
using OrderBridge.Integrations.Infrastructure.Persistence;

namespace OrderBridge.Integrations.Infrastructure.Tests;
public sealed class PersistenceTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    [Fact]
    public async Task Concurrent_integration_update_is_rejected()
    {
        var order = await CreateSnapshotAsync();
        var task = OrderIntegration.Create(Guid.NewGuid(), order.Id, IntegrationTarget.OneC,
            IntegrationOperation.CreateSalesOrder, Guid.NewGuid().ToString(), DateTimeOffset.UtcNow, false);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IntegrationsDbContext>();
            db.Add(task);
            await db.SaveChangesAsync();
        }
        using var first = fixture.Services.CreateScope();
        using var second = fixture.Services.CreateScope();
        var a = first.ServiceProvider.GetRequiredService<IntegrationsDbContext>();
        var b = second.ServiceProvider.GetRequiredService<IntegrationsDbContext>();
        var firstTask = await a.OrderIntegrations.SingleAsync(x => x.Id == task.Id);
        var secondTask = await b.OrderIntegrations.SingleAsync(x => x.Id == task.Id);
        firstTask.StartAttempt(DateTimeOffset.UtcNow);
        secondTask.StartAttempt(DateTimeOffset.UtcNow);
        await a.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => b.SaveChangesAsync());
    }

    [Fact]
    public async Task Get_paged_integrations_filters_by_order_and_reports_total_count()
    {
        var order = await CreateSnapshotAsync();
        var createdAt = DateTimeOffset.UtcNow;
        var first = OrderIntegration.Create(
            Guid.NewGuid(), order.Id, IntegrationTarget.OneC,
            IntegrationOperation.CreateSalesOrder, Guid.NewGuid().ToString("N"),
            createdAt, isWaitingForPrerequisite: false);
        var second = OrderIntegration.Create(
            Guid.NewGuid(), order.Id, IntegrationTarget.Diadoc,
            IntegrationOperation.CreateDocument, Guid.NewGuid().ToString("N"),
            createdAt, isWaitingForPrerequisite: true);

        using (var writeScope = fixture.Services.CreateScope())
        {
            var dbContext = writeScope.ServiceProvider.GetRequiredService<IntegrationsDbContext>();
            dbContext.AddRange(first, second);
            await dbContext.SaveChangesAsync();
        }

        using var readScope = fixture.Services.CreateScope();
        var repository = readScope.ServiceProvider
            .GetRequiredService<IOrderIntegrationReadRepository>();

        var page = await repository.GetPagedAsync(
            new PaginationRequest(pageNumber: 1, pageSize: 1),
            new OrderIntegrationsFilter(OrderId: order.Id),
            OrderIntegrationsSortBy.CreatedAt,
            SortDirection.Ascending);

        Assert.Equal(2, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
        Assert.Single(page.Items);
        Assert.Equal(order.Id, page.Items[0].OrderId);
        Assert.True(page.HasNextPage);
    }

    [Fact]
    public async Task Integration_attempt_repository_persists_tracks_and_filters_attempts()
    {
        var order = await CreateSnapshotAsync();
        var integration = OrderIntegration.Create(
            Guid.NewGuid(), order.Id, IntegrationTarget.Telegram,
            IntegrationOperation.SendOrderReceivedNotification,
            Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow,
            isWaitingForPrerequisite: false);
        var startedAt = DateTimeOffset.UtcNow;

        integration.StartAttempt(startedAt);
        var attempt = IntegrationAttempt.Start(
            integration,
            startedAt,
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa1");
        attempt.Complete(
            new IntegrationExecutionResult.IntegrationTransientFailure(
                "telegram.temporarily_unavailable",
                "Telegram is temporarily unavailable.",
                503,
                TimeSpan.FromMinutes(1)),
            startedAt.AddSeconds(1));

        var secondIntegration = OrderIntegration.Create(
            Guid.NewGuid(), order.Id, IntegrationTarget.OneC,
            IntegrationOperation.CreateSalesOrder,
            Guid.NewGuid().ToString("N"), startedAt,
            isWaitingForPrerequisite: false);
        secondIntegration.StartAttempt(startedAt.AddMilliseconds(1));
        var secondAttempt = IntegrationAttempt.Start(
            secondIntegration,
            startedAt.AddMilliseconds(1),
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa2");
        secondAttempt.Complete(
            new IntegrationExecutionResult.IntegrationSucceeded("1C-42", 200),
            startedAt.AddSeconds(2));

        using (var writeScope = fixture.Services.CreateScope())
        {
            var services = writeScope.ServiceProvider;
            services.GetRequiredService<IRepository<OrderIntegration, Guid>>().Add(integration);
            services.GetRequiredService<IRepository<OrderIntegration, Guid>>().Add(secondIntegration);
            services.GetRequiredService<IIntegrationAttemptRepository>().Add(attempt);
            services.GetRequiredService<IIntegrationAttemptRepository>().Add(secondAttempt);
            await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        }

        using (var trackedScope = fixture.Services.CreateScope())
        {
            var repository = trackedScope.ServiceProvider
                .GetRequiredService<IIntegrationAttemptRepository>();
            var loaded = await repository.GetByIdAsync(attempt.Id);

            Assert.NotNull(loaded);
            Assert.Equal(503, loaded.ResponseStatusCode);
            Assert.Equal(IntegrationAttemptOutcome.Failed, loaded.Outcome);
            Assert.Contains(
                trackedScope.ServiceProvider.GetRequiredService<IntegrationsDbContext>()
                    .ChangeTracker.Entries<IntegrationAttempt>(),
                entry => entry.Entity.Id == attempt.Id);
        }

        using var readScope = fixture.Services.CreateScope();
        var readRepository = readScope.ServiceProvider
            .GetRequiredService<IIntegrationAttemptRepository>();
        var page = await readRepository.GetPagedAsync(
            new PaginationRequest(pageNumber: 1, pageSize: 1),
            new IntegrationAttemptFilter(
                OrderIntegrationId: integration.Id,
                Outcome: IntegrationAttemptOutcome.Failed,
                ErrorCodeSearchTerm: "temporarily",
                SearchTerm: "503"));

        Assert.Equal(1, page.TotalCount);
        Assert.Equal(attempt.Id, Assert.Single(page.Items).Id);

        var sortedPage = await readRepository.GetPagedAsync(
            new PaginationRequest(pageNumber: 1, pageSize: 2),
            new IntegrationAttemptFilter(TraceIdSearchTerm: "aaaaaaaa"),
            IntegrationAttemptSortBy.ResponseStatusCode,
            SortDirection.Ascending);

        Assert.Equal([200, 503], sortedPage.Items.Select(item => item.ResponseStatusCode));
        Assert.Empty(readScope.ServiceProvider.GetRequiredService<IntegrationsDbContext>()
            .ChangeTracker.Entries());
    }

    private async Task<OrderExecutionSnapshot> CreateSnapshotAsync()
    {
        var snapshot = new OrderExecutionSnapshot(Guid.NewGuid(), "test", Guid.NewGuid().ToString(),
            new("Company", "123", "a@example.com"), "RUB", 10, [new("SKU", "Item", 1, 10)]);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IntegrationsDbContext>();
        db.IntegrationOrders.Add(new IntegrationOrder { Id = snapshot.Id, ReceivedAt = DateTimeOffset.UtcNow,
            Payload = System.Text.Json.JsonSerializer.Serialize(snapshot) });
        await db.SaveChangesAsync();
        return snapshot;
    }
}
