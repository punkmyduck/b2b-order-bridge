using System.Diagnostics;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderBridge.Application.Features.OrderIntegrations.Commands;
using OrderBridge.Application.Features.OrderIntegrations.Services.Interfaces;
using Shared.Application.Services;

namespace OrderBridge.Infrastructure.BackgroundJobs;

public sealed class IntegrationWorker(
    IServiceScopeFactory scopeFactory,
    IDateTimeOffsetProvider dateTimeOffsetProvider,
    ILogger<IntegrationWorker> logger) : BackgroundService
{
    public const string ActivitySourceName = "OrderBridge.Integrations";
    public const int BatchSize = 50;
    public const int MaxDegreeOfParallelism = 8;

    private static readonly TimeSpan Period = TimeSpan.FromSeconds(10);
    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Integration worker started.");

        using var timer = new PeriodicTimer(Period);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Integration worker batch failed.");
            }

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                    break;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        logger.LogInformation("Integration worker stopped.");
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        var integrationIds = await GetReadyIntegrationIdsAsync(cancellationToken);

        await Parallel.ForEachAsync(
            integrationIds,
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = MaxDegreeOfParallelism
            },
            (integrationId, token) =>
                new ValueTask(ProcessIntegrationAsync(integrationId, token)));
    }

    private async Task<IReadOnlyList<Guid>> GetReadyIntegrationIdsAsync(
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var jobStore = scope.ServiceProvider.GetRequiredService<IIntegrationJobStore>();

        return await jobStore.GetReadyIntegrationIdsAsync(
            dateTimeOffsetProvider.UtcNow,
            BatchSize,
            cancellationToken);
    }

    private async Task ProcessIntegrationAsync(
        Guid integrationId,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity(
                                 "ProcessOrderIntegration",
                                 ActivityKind.Internal)
                             ?? new Activity("ProcessOrderIntegration")
                                 .SetIdFormat(ActivityIdFormat.W3C)
                                 .Start();
        activity.SetTag("order.integration.id", integrationId);

        await using var scope = scopeFactory.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        try
        {
            var result = await sender.Send(
                new ProcessOrderIntegrationCommand(integrationId),
                cancellationToken);

            if (result.IsFailure)
            {
                logger.LogWarning(
                    "Integration {IntegrationId} was not processed: {ErrorCode} {ErrorDescription}",
                    integrationId,
                    result.Error!.Code,
                    result.Error.Description);
            }
        }
        catch (DbUpdateConcurrencyException)
        {
            logger.LogDebug(
                "Integration {IntegrationId} was claimed by another worker.",
                integrationId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Unexpected error while processing integration {IntegrationId}.",
                integrationId);
        }
    }
}
