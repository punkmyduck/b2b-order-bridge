using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
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
using Shared.Messaging.Abstractions.Events;
using Shared.Messaging.Abstractions.Processing;

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
        var integration = await db.OrderIntegrations.SingleAsync(x => x.OrderId == message.Payload.Order.Id && x.IntegrationTarget == IntegrationTarget.Telegram);
        var capture = new CaptureHandler();
        var handler = new ProcessOrderIntegrationCommandHandler(
            services.GetRequiredService<IDateTimeOffsetProvider>(),
            services.GetRequiredService<IRepository<OrderIntegration, Guid>>(),
            services.GetRequiredService<IIntegrationOrderSnapshotRepository>(),
            services.GetRequiredService<IIntegrationAttemptRepository>(),
            services.GetRequiredService<IUnitOfWork>(),
            new FixedResolver(capture),
            new BaseOrderIntegrationRetryPolicy(),
            NullLogger<ProcessOrderIntegrationCommandHandler>.Instance);
        var result = await handler.Handle(new(integration.Id), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(message.Payload.Order.Customer.CompanyName, capture.Context!.Order.Customer.CompanyName);
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

        Assert.Equal(4, await db.OrderIntegrations.CountAsync(x => x.OrderId == message.Payload.Order.Id));
        var snapshot = await scope.ServiceProvider.GetRequiredService<IIntegrationOrderSnapshotRepository>().GetByIdAsync(message.Payload.Order.Id);

        Assert.Equal(message.Payload.Order.Customer.CompanyName, snapshot!.Customer.CompanyName);
        Assert.Equal(message.Payload.Order.Lines[0].Sku, Assert.Single(snapshot.Lines).Sku);
        Assert.DoesNotContain(db.Model.GetEntityTypes(), type => type.GetTableName() == "Orders");
        Assert.DoesNotContain(db.Model.GetEntityTypes(), type => type.GetTableName() == "OutboxMessages");
    }

    [Fact]
    public async Task Concurrent_and_rewrapped_duplicates_keep_original_integration_keys()
    {
        var message = CreateEvent();

        await Task.WhenAll(ProcessAsync(message), ProcessAsync(message));
        var before = await KeysAsync(message.Payload.Order.Id);

        await ProcessAsync(message);
        await ProcessAsync(message with { Metadata = message.Metadata with { EventId = Guid.NewGuid() } });
        Assert.Equal(4, before.Length);
        Assert.Equal(before, await KeysAsync(message.Payload.Order.Id));
    }

    [Fact]
    public async Task Failed_plan_rolls_back_receipt_projection_and_jobs()
    {
        var message = CreateEvent();

        using (var scope = fixture.Services.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IIntegrationPlanningStore>();
            var invalidJob = OrderIntegration.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                IntegrationTarget.OneC,
                IntegrationOperation.CreateSalesOrder,
                "invalid-plan",
                message.Metadata.OccurredAt,
                false);

            await Assert.ThrowsAsync<DbUpdateException>(() => scope.ServiceProvider.GetRequiredService<IInboxProcessor>().ProcessAsync(
                "order-created-integration-planner-v1",
                message,
                ct => store.SaveAsync(
                    OrderExecutionSnapshot.FromEvent(message.Payload.Order),
                    [invalidJob],
                    ct),
                default));
        }

        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IntegrationsDbContext>();

            Assert.False(await db.InboxMessages.AnyAsync(x => x.EventId == message.Metadata.EventId));
            Assert.False(await db.IntegrationOrders.AnyAsync(x => x.Id == message.Payload.Order.Id));
        }

        await ProcessAsync(message);
        Assert.Equal(4, (await KeysAsync(message.Payload.Order.Id)).Length);
    }

    [Fact]
    public void Invalid_event_and_unknown_scenario_are_rejected()
    {
        var message = CreateEvent();

        Assert.Throws<InvalidEventException>(() => EventValidation.Validate(
            message with { Metadata = message.Metadata with { EventId = Guid.Empty } },
            OrderCreatedEvent.Definition));
        Assert.Throws<InvalidEventException>(() => OrderCreatedHandler.ReadScenario(message.Payload with { ProcessingScenario = "999" }));
    }

    [Fact]
    public async Task Same_idempotency_key_with_new_event_id_does_not_invoke_handler_again()
    {
        var message = CreateEvent();
        var calls = 0;

        foreach (var id in new[]

            {
                message.Metadata.EventId,
                Guid.NewGuid()
            }

        )
        {
            using var scope = fixture.Services.CreateScope();

            await scope.ServiceProvider.GetRequiredService<IInboxProcessor>().ProcessAsync(
                "idempotency-test",
                message with { Metadata = message.Metadata with { EventId = id } },
                _ =>
                {
                    calls++;

                    return Task.CompletedTask;
                },
                default);
        }

        Assert.Equal(1, calls);
        using var read = fixture.Services.CreateScope();
        var saved = await read.ServiceProvider.GetRequiredService<IntegrationsDbContext>().InboxMessages.SingleAsync(x => x.Consumer == "idempotency-test" && x.EventId == message.Metadata.EventId);

        Assert.NotNull(saved.Payload);
        Assert.Equal(message.Metadata.IdempotencyKey, saved.IdempotencyKey);
        Assert.Equal(message.Metadata.OrderingKey, saved.OrderingKey);
    }

    [Fact]
    public async Task Invalid_nested_payload_rolls_back_inbox_and_is_a_permanent_input_failure()
    {
        var message = CreateEvent();

        message = message with

        {
            Payload = message.Payload with

            {
                Order = message.Payload.Order with
                {
                    Lines = [null!]
                }
            }
        };
        await Assert.ThrowsAsync<InvalidEventException>(() => ProcessAsync(message));
        using var scope = fixture.Services.CreateScope();

        Assert.False(await scope.ServiceProvider.GetRequiredService<IntegrationsDbContext>().InboxMessages.AnyAsync(x => x.EventId == message.Metadata.EventId));
    }

    [Fact]
    public async Task Migration_preserves_legacy_inbox_receipt_and_suppresses_redelivery()
    {
        await using var postgres = new Testcontainers.PostgreSql.PostgreSqlBuilder("postgres:17-alpine").Build();

        await postgres.StartAsync();
        await using var db = new IntegrationsDbContext(new DbContextOptionsBuilder<IntegrationsDbContext>().UseNpgsql(postgres.GetConnectionString()).Options);
        var migrator = db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>();

        await migrator.MigrateAsync("20261019143359_InitialIntegrations");
        var message = CreateEvent();
        var id = message.Metadata.EventId;
        var now = DateTimeOffset.UtcNow;

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "InboxMessages" ("Consumer", "EventId", "ProcessedAt")
            VALUES ('order-created-integration-planner-v1', {id}, {now})
            """);
        await migrator.MigrateAsync();
        var calls = 0;

        await new Shared.Messaging.Persistence.EfInboxProcessor<IntegrationsDbContext>(db, TimeProvider.System).ProcessAsync(
            "order-created-integration-planner-v1",
            message,
            _ =>
            {
                calls++;

                return Task.CompletedTask;
            },
            default);
        Assert.Equal(0, calls);
        var saved = await db.InboxMessages.SingleAsync();

        Assert.Equal(id.ToString("N"), saved.IdempotencyKey);
        Assert.Null(saved.Payload);
    }

    private async Task ProcessAsync(IntegrationEvent<OrderCreatedV1> message)
    {
        using var scope = fixture.Services.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<IEventHandler<OrderCreatedV1>>();

        await scope.ServiceProvider.GetRequiredService<IInboxProcessor>().ProcessAsync(
            "order-created-integration-planner-v1",
            message,
            ct => handler.HandleAsync(message, ct),
            default);
    }

    private async Task<string[]> KeysAsync(Guid orderId)
    {
        using var scope = fixture.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<IntegrationsDbContext>().OrderIntegrations.Where(x => x.OrderId == orderId)
            .OrderBy(x => x.IntegrationTarget)
            .Select(x => x.IdempotencyKey).ToArrayAsync();
    }

    private static IntegrationEvent<OrderCreatedV1> CreateEvent()
    {
        var orderId = Guid.NewGuid();

        return IntegrationEvent<OrderCreatedV1>.Create(
            OrderCreatedEvent.Definition,
            new(
                "FullCycle",
                new(
                    orderId,
                    "isolated-test",
                    Guid.NewGuid().ToString(),
                    new(
                        "Company",
                        "123",
                        "test@example.com"),
                    "RUB",
                    10,
                    [new(
                        Guid.NewGuid(),
                        "SKU",
                        "Item",
                        1,
                        10,
                        10)])),
            "orders",
            orderId.ToString("N"),
            DateTimeOffset.UtcNow);
    }

    private sealed class FixedResolver(IOrderIntegrationHandler handler) : IOrderIntegrationResolver
    {
        public IOrderIntegrationHandler Resolve(
            IntegrationTarget target,
            IntegrationOperation operation) => handler;
    }

    private sealed class CaptureHandler : IOrderIntegrationHandler
    {
        public IntegrationExecutionContext? Context { get; private set; }

        public IntegrationTarget IntegrationTarget => IntegrationTarget.Telegram;

        public IntegrationOperation IntegrationOperation => IntegrationOperation.SendOrderReceivedNotification;

        public Task<IntegrationExecutionResult> ExecuteAsync(
            IntegrationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            Context = executionContext;

            return Task.FromResult<IntegrationExecutionResult>(new IntegrationExecutionResult.IntegrationSucceeded("telegram-test", 200));
        }
    }
}
