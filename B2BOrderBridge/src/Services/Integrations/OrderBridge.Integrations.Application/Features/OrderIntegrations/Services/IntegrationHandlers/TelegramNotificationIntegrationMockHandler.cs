using OrderBridge.Integrations.Application.Features.OrderIntegrations.Services.Interfaces;
using OrderBridge.Integrations.Application.Features.OrderIntegrations.ValueObjects;
using OrderBridge.Integrations.Application.ValueObjects;
using OrderBridge.Integrations.Domain.ValueObjects;

namespace OrderBridge.Integrations.Application.Features.OrderIntegrations.Services.IntegrationHandlers;

public sealed class TelegramNotificationIntegrationMockHandler : IOrderIntegrationHandler
{
    public IntegrationTarget IntegrationTarget => IntegrationTarget.Telegram;

    public IntegrationOperation IntegrationOperation => IntegrationOperation.SendOrderReceivedNotification;


    public Task<IntegrationExecutionResult> ExecuteAsync(
        IntegrationExecutionContext executionContext,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var randomValue = Random.Shared.Next(0, 10);
        IntegrationExecutionResult result;

        if (randomValue < 5)
        {
            Console.WriteLine($"Успешно доставлено сообщение по execution context:\n{executionContext}");
            result = new IntegrationExecutionResult.IntegrationSucceeded(
                Random.Shared.Next(0, 999999999).ToString(),
                null);
        }
        else if (randomValue < 8)
        {
            Console.WriteLine($"Не удалось отправить сообщение, повторите попытку позже. Context:\n{executionContext}");
            result = new IntegrationExecutionResult.IntegrationTransientFailure(
                "telegram.temporarily_unavailable",
                "The mock Telegram service is temporarily unavailable.",
                503,
                TimeSpan.FromSeconds(60));
        }
        else
        {
            Console.WriteLine($"Неустранимая ошибка отправки. Context:\n{executionContext}");
            result = new IntegrationExecutionResult.IntegrationPermanentFailure(
                "telegram.request_rejected",
                "The mock Telegram service rejected the notification.",
                400);
        }

        return Task.FromResult(result);
    }
}
