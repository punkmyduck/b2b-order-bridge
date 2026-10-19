using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using OrderBridge.Web.Models;

namespace OrderBridge.Web.Services;

public sealed class OrderBridgeApiClient(HttpClient http, ILogger<OrderBridgeApiClient> logger)
{
    public Task<PageResult<OrderSummary>> GetOrdersAsync(int page, int pageSize, string? search,
        string sort, string direction, CancellationToken cancellationToken = default)
        => GetAsync<PageResult<OrderSummary>>(
            $"api/orders?PageNumber={page}&PageSize={pageSize}&SortBy={Uri.EscapeDataString(sort)}" +
            $"&SortDirection={Uri.EscapeDataString(direction)}&SearchTerm={Uri.EscapeDataString(search ?? "")}", cancellationToken);

    public Task<OrderDetails> GetOrderAsync(Guid id, CancellationToken cancellationToken = default)
        => GetAsync<OrderDetails>($"api/orders/{id}/details", cancellationToken);

    public Task<PageResult<Attempt>> GetAttemptsAsync(Guid integrationId, int page,
        CancellationToken cancellationToken = default)
        => GetAsync<PageResult<Attempt>>(
            $"api/order-integrations/attempts?OrderIntegrationId={integrationId}&PageNumber={page}" +
            "&PageSize=5&SortBy=StartedAt&SortDirection=Descending", cancellationToken);

    public async Task<CreatedOrder> CreateOrderAsync(CreateOrderInput input, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/orders")
        {
            Content = JsonContent.Create(input)
        };
        return await SendAsync<CreatedOrder>(request, cancellationToken);
    }

    private async Task<T> GetAsync<T>(string url, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        return await SendAsync<T>(request, cancellationToken);
    }

    private async Task<T> SendAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                ApiProblem? problem = null;
                try { problem = await response.Content.ReadFromJsonAsync<ApiProblem>(cancellationToken); }
                catch (JsonException) { /* Proxies may return HTML instead of ProblemDetails. */ }

                var message = response.StatusCode switch
                {
                    HttpStatusCode.NotFound => "Заказ или запись не найдены.",
                    HttpStatusCode.Conflict => "Заказ с таким источником и внешним номером уже существует.",
                    HttpStatusCode.BadRequest => "Проверьте введённые данные.",
                    _ => "Не удалось выполнить запрос. Попробуйте ещё раз."
                };
                throw new ApiException(response.StatusCode, message, problem?.Errors, problem?.Detail, problem?.TraceId);
            }
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken)
                ?? throw new ApiException(response.StatusCode, "API вернул пустой ответ.");
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "OrderBridge API is unavailable");
            throw new ApiException(null, "Нет связи с API. Проверьте, что backend запущен, и повторите запрос.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ApiException(null, "API не ответил вовремя. Обновите список перед повторным созданием заказа.");
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Invalid OrderBridge API response");
            throw new ApiException(null, "Не удалось прочитать ответ API.");
        }
    }

    private sealed record ApiProblem(string? Detail, Dictionary<string, string[]>? Errors, string? TraceId);
}

public sealed class ApiException(HttpStatusCode? status, string message,
    IReadOnlyDictionary<string, string[]>? errors = null, string? detail = null, string? traceId = null)
    : Exception(message)
{
    public HttpStatusCode? Status { get; } = status;
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors ?? new Dictionary<string, string[]>();
    public string? Detail { get; } = detail;
    public string? TraceId { get; } = traceId;
}
