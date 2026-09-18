using Microsoft.Extensions.Logging;
using OrderBridge.Application.Features.OrderIntegrations.Services.Interfaces;
using OrderBridge.Application.Features.OrderIntegrations.ValueObjects;
using OrderBridge.Application.ValueObjects;
using OrderBridge.Domain.Models;
using Shared.Application.Messaging;
using Shared.Application.Persistence;
using Shared.Application.Results;
using Shared.Application.Services;

namespace OrderBridge.Application.Features.OrderIntegrations.Commands;

public sealed record ProcessOrderIntegrationCommand(Guid IntegrationId) : ICommand;

public sealed class ProcessOrderIntegrationCommandHandler(
    IDateTimeOffsetProvider dateTimeOffsetProvider,
    IRepository<OrderIntegration, Guid> integrationRepository,
    IRepository<Order, Guid> orderRepository,
    IUnitOfWork unitOfWork,
    IOrderIntegrationResolver resolver,
    IOrderIntegrationRetryPolicy retryPolicy,
    ILogger<ProcessOrderIntegrationCommandHandler> logger)
    : ICommandHandler<ProcessOrderIntegrationCommand>
{
    public async Task<Result> Handle(
        ProcessOrderIntegrationCommand command,
        CancellationToken cancellationToken)
    {
        var integration = await integrationRepository.GetByIdAsync(
            command.IntegrationId,
            cancellationToken);

        if (integration is null)
        {
            return new Error(
                "OrderIntegration.NotFound",
                $"Order integration '{command.IntegrationId}' was not found.",
                ErrorType.NotFound);
        }

        var order = await orderRepository.GetByIdAsync(
            integration.OrderId,
            cancellationToken);

        if (order is null)
        {
            return new Error(
                "Order.NotFound",
                $"Order '{integration.OrderId}' was not found.",
                ErrorType.NotFound);
        }

        IOrderIntegrationHandler handler;
        try
        {
            handler = resolver.Resolve(
                integration.IntegrationTarget,
                integration.IntegrationOperation);
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(
                exception,
                "No handler is available for integration {IntegrationId}.",
                integration.Id);

            return new Error(
                "OrderIntegration.HandlerNotFound",
                "No handler is registered for the integration.");
        }

        var startedAt = dateTimeOffsetProvider.UtcNow;
        var executionContext = new IntegrationExecutionContext(
            integration.Id,
            integration.IdempotencyKey,
            integration.AttemptCount + 1,
            OrderExecutionSnapshot.FromDomain(order));

        integration.StartAttempt(startedAt);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var executionResult = await ExecuteAsync(
            handler,
            executionContext,
            integration.Id,
            cancellationToken);

        var completedAt = dateTimeOffsetProvider.UtcNow;
        ApplyExecutionResult(integration, executionResult, completedAt);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private async Task<IntegrationExecutionResult> ExecuteAsync(
        IOrderIntegrationHandler handler,
        IntegrationExecutionContext context,
        Guid integrationId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await handler.ExecuteAsync(context, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Integration handler threw an unexpected exception for integration {IntegrationId}.",
                integrationId);

            return new IntegrationExecutionResult.IntegrationTransientFailure(
                "integration.unexpected_error",
                "The integration handler failed unexpectedly.",
                null,
                null);
        }
    }

    private void ApplyExecutionResult(
        OrderIntegration integration,
        IntegrationExecutionResult result,
        DateTimeOffset completedAt)
    {
        switch (result)
        {
            case IntegrationExecutionResult.IntegrationSucceeded succeeded:
                integration.MarkSucceeded(succeeded.ExternalEntityId, completedAt);
                break;

            case IntegrationExecutionResult.IntegrationTransientFailure failure
                when retryPolicy.CanRetry(integration.AttemptCount):
                integration.ScheduleRetry(
                    failure.Code,
                    failure.Message,
                    retryPolicy.GetNextAttemptAt(
                        integration.AttemptCount,
                        completedAt,
                        failure.RetryAfter),
                    completedAt);
                break;

            case IntegrationExecutionResult.IntegrationTransientFailure failure:
                integration.MarkFailed(failure.Code, failure.Message, completedAt);
                break;

            case IntegrationExecutionResult.IntegrationPermanentFailure failure:
                integration.MarkFailed(failure.Code, failure.Message, completedAt);
                break;

            default:
                integration.MarkFailed(
                    "integration.invalid_result",
                    "The integration handler returned an unsupported result.",
                    completedAt);
                break;
        }
    }
}
