using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderBridge.Contracts.Events;
using OrderBridge.Integrations.Application.Messaging;
using OrderBridge.Integrations.Application.Features.OrderIntegrations.Repositories;
using OrderBridge.Integrations.Application.Features.OrderIntegrations.Services.Implementations;
using OrderBridge.Integrations.Application.Features.OrderIntegrations.ValueObjects;
using OrderBridge.Integrations.Domain.Models;
using OrderBridge.Integrations.Domain.ValueObjects;
using OrderBridge.Integrations.Infrastructure.Persistence;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;
using OrderBridge.Integrations.Application.Features.OrderIntegrations.Commands;
using OrderBridge.Integrations.Application.Features.OrderIntegrations.Services.Interfaces;
using OrderBridge.Integrations.Application.ValueObjects;
using Shared.Application.Persistence;
using Shared.Application.Services;

namespace OrderBridge.Integrations.Infrastructure.Tests;

public sealed class InboxTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    [Fact]
    public async Task Executor_uses_local_event_snapshot_and_persists_attempt_without_orders_service()
    {
        var message = CreateEvent();
        await ProcessAsync(message);
        using var scope = fixture.Services.CreateScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<IntegrationsDbContext>();
        var integration = await db.OrderIntegrations.SingleAsync(x => x.OrderId == message.Order.Id && x.IntegrationTarget == IntegrationTarget.Telegram);
        var capture = new CaptureHandler();
        var handler = new ProcessOrderIntegrationCommandHandler(services.GetRequiredService<IDateTimeOffsetProvider>(),
            services.GetRequiredService<IRepository<OrderIntegration, Guid>>(),
            services.GetRequiredService<IIntegrationOrderSnapshotRepository>(),
            services.GetRequiredService<IIntegrationAttemptRepository>(), services.GetRequiredService<IUnitOfWork>(),
            new FixedResolver(capture), new BaseOrderIntegrationRetryPolicy(), NullLogger<ProcessOrderIntegrationCommandHandler>.Instance);
        var result = await handler.Handle(new(integration.Id), default);
        Assert.True(result.IsSuccess);
        Assert.Equal(message.Order.Customer.CompanyName, capture.Context!.Order.Customer.CompanyName);
        Assert.Equal(IntegrationStatus.Succeeded, integration.IntegrationStatus);
        Assert.Equal(1, await db.IntegrationAttempts.CountAsync(x => x.OrderIntegrationId == integration.Id));
    }

    [Fact]
    public async Task Event_creates_local_snapshot_and_integrations_without_an_orders_database()
    {
        var message = CreateEvent();
        await ProcessAsync(message);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IntegrationsDbContext>();
        Assert.Equal(4, await db.OrderIntegrations.CountAsync(x => x.OrderId == message.Order.Id));
        var snapshot = await scope.ServiceProvider.GetRequiredService<IIntegrationOrderSnapshotRepository>().GetByIdAsync(message.Order.Id);
        Assert.Equal(message.Order.Customer.CompanyName, snapshot!.Customer.CompanyName);
        Assert.Equal(message.Order.Lines[0].Sku, Assert.Single(snapshot.Lines).Sku);
        Assert.DoesNotContain(db.Model.GetEntityTypes(), type => type.GetTableName() == "Orders");
        Assert.DoesNotContain(db.Model.GetEntityTypes(), type => type.GetTableName() == "OutboxMessages");
    }

    [Fact]
    public async Task Concurrent_and_rewrapped_duplicates_keep_original_integration_keys()
    {
        var message = CreateEvent();
        await Task.WhenAll(ProcessAsync(message), ProcessAsync(message));
        var before = await KeysAsync(message.Order.Id);
        await ProcessAsync(message);
        await ProcessAsync(message with { EventId = Guid.NewGuid() });
        Assert.Equal(4, before.Length);
        Assert.Equal(before, await KeysAsync(message.Order.Id));
    }

    [Fact]
    public async Task Failed_plan_rolls_back_receipt_projection_and_jobs()
    {
        var message = CreateEvent();
        using (var scope = fixture.Services.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IIntegrationPlanningStore>();
            var invalidJob = OrderIntegration.Create(Guid.NewGuid(), Guid.NewGuid(), IntegrationTarget.OneC,
                IntegrationOperation.CreateSalesOrder, "invalid-plan", message.OccurredAt, false);
            await Assert.ThrowsAsync<DbUpdateException>(() => store.SaveOnceAsync(message.EventId,
                OrderExecutionSnapshot.FromEvent(message.Order), [invalidJob], default));
        }
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IntegrationsDbContext>();
            Assert.False(await db.InboxMessages.AnyAsync(x => x.EventId == message.EventId));
            Assert.False(await db.IntegrationOrders.AnyAsync(x => x.Id == message.Order.Id));
        }
        await ProcessAsync(message);
        Assert.Equal(4, (await KeysAsync(message.Order.Id)).Length);
    }

    [Fact]
    public void Invalid_event_and_unknown_scenario_are_rejected()
    {
        var message = CreateEvent();
        Assert.Throws<System.Text.Json.JsonException>(() => OrderCreatedProcessor.ReadScenario(message with { EventId = Guid.Empty }));
        Assert.Throws<System.Text.Json.JsonException>(() => OrderCreatedProcessor.ReadScenario(message with { ProcessingScenario = "999" }));
    }

    private async Task ProcessAsync(OrderCreatedV1 message)
    {
        using var scope = fixture.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<OrderCreatedProcessor>().ProcessAsync(message, default);
    }
    private async Task<string[]> KeysAsync(Guid orderId)
    {
        using var scope = fixture.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IntegrationsDbContext>().OrderIntegrations
            .Where(x => x.OrderId == orderId).OrderBy(x => x.IntegrationTarget).Select(x => x.IdempotencyKey).ToArrayAsync();
    }
    private static OrderCreatedV1 CreateEvent() => new(Guid.NewGuid(), DateTimeOffset.UtcNow, "FullCycle",
        new(Guid.NewGuid(), "isolated-test", Guid.NewGuid().ToString(), new("Company", "123", "test@example.com"),
            "RUB", 10, [new(Guid.NewGuid(), "SKU", "Item", 1, 10, 10)]));

    private sealed class FixedResolver(IOrderIntegrationHandler handler) : IOrderIntegrationResolver
    {
        public IOrderIntegrationHandler Resolve(IntegrationTarget target, IntegrationOperation operation) => handler;
    }
    private sealed class CaptureHandler : IOrderIntegrationHandler
    {
        public IntegrationExecutionContext? Context { get; private set; }
        public IntegrationTarget IntegrationTarget => IntegrationTarget.Telegram;
        public IntegrationOperation IntegrationOperation => IntegrationOperation.SendOrderReceivedNotification;
        public Task<IntegrationExecutionResult> ExecuteAsync(IntegrationExecutionContext executionContext, CancellationToken cancellationToken)
        {
            Context = executionContext;
            return Task.FromResult<IntegrationExecutionResult>(new IntegrationExecutionResult.IntegrationSucceeded("telegram-test", 200));
        }
    }
}
