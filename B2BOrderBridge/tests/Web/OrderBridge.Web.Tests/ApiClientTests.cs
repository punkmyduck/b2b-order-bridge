using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using OrderBridge.Web.Models;
using OrderBridge.Web.Services;
using Xunit;

namespace OrderBridge.Web.Tests;

public sealed class ApiClientTests
{
    [Fact]
    public async Task Detail_composes_orders_and_integrations_from_different_hosts()
    {
        var id = Guid.NewGuid();
        var orders = new StubHandler(request =>
        {
            Assert.Equal("orders", request.RequestUri!.Host);
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(Order(id)) };
        });
        var integrations = new StubHandler(request =>
        {
            Assert.Equal("integrations", request.RequestUri!.Host);
            Assert.Equal("/api/order-integrations", request.RequestUri.AbsolutePath);
            Assert.Contains($"OrderId={id}", request.RequestUri.Query);
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new PageResult<OrderIntegration>([], 0, 1, 100, 0, false, false)) };
        });
        var client = new OrderBridgeApiClient(new HttpClient(orders) { BaseAddress = new("http://orders/") },
            new HttpClient(integrations) { BaseAddress = new("http://integrations/") }, NullLogger<OrderBridgeApiClient>.Instance);
        var result = await client.GetOrderAsync(id);
        Assert.Equal(id, result.Id);
        Assert.True(result.IntegrationsAvailable);
    }

    [Fact]
    public async Task Unavailable_integrations_do_not_hide_order_details()
    {
        var id = Guid.NewGuid();
        var orders = new StubHandler(_ => new(HttpStatusCode.OK) { Content = JsonContent.Create(Order(id)) });
        var integrations = new StubHandler(_ => new(HttpStatusCode.ServiceUnavailable));
        var client = new OrderBridgeApiClient(new HttpClient(orders) { BaseAddress = new("http://orders/") },
            new HttpClient(integrations) { BaseAddress = new("http://integrations/") }, NullLogger<OrderBridgeApiClient>.Instance);
        var result = await client.GetOrderAsync(id);
        Assert.Equal(id, result.Id);
        Assert.False(result.IntegrationsAvailable);
        Assert.Empty(result.Integrations);
    }

    private static OrderDetails Order(Guid id) => new(id, "portal", "42", new("Company", "123", "a@example.com"),
        "Received", "Pending", 10, "RUB", DateTimeOffset.UtcNow, null, [], []);

    [Fact]
    public async Task Search_does_not_inject_query_parameters()
    {
        var handler = new StubHandler(request =>
        {
            Assert.Equal("/api/orders", request.RequestUri!.AbsolutePath);
            Assert.Contains("SearchTerm=" + Uri.EscapeDataString("РћРћРћ & PageNumber=999"), request.RequestUri.Query);
            Assert.Contains("PageNumber=2&PageSize=10", request.RequestUri.Query);
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new PageResult<OrderSummary>([], 0, 2, 10, 0, false, false)) };
        });
        var result = await Client(handler).GetOrdersAsync(2, 10, "РћРћРћ & PageNumber=999", "CreatedAt", "Descending");
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task Create_sends_the_contract_and_reads_created_id()
    {
        var id = Guid.NewGuid();
        var handler = new StubHandler(request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/api/orders", request.RequestUri!.AbsolutePath);
            return new(HttpStatusCode.Created) { Content = JsonContent.Create(new CreatedOrder(id)) };
        });
        var result = await Client(handler).CreateOrderAsync(new("Web", "ORDER-1", new("Company", "123", "a@example.com"), "RUB", [new("SKU", "Item", 1, 50)]));
        Assert.Equal(id, result.Id);
        var input = await handler.Body!.ReadFromJsonAsync<CreateOrderInput>();
        Assert.Equal("ORDER-1", input!.ExternalOrderId);
        Assert.Single(input.Lines);
    }

    [Fact]
    public async Task Validation_errors_and_trace_id_are_preserved()
    {
        var handler = new StubHandler(_ => new(HttpStatusCode.BadRequest)
        {
            Content = JsonContent.Create(new { errors = new Dictionary<string, string[]> { ["Currency"] = ["Unsupported currency"] }, traceId = "trace-1" })
        });
        var error = await Assert.ThrowsAsync<ApiException>(() => Client(handler).GetOrderAsync(Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.BadRequest, error.Status);
        Assert.Equal("Unsupported currency", error.Errors["Currency"][0]);
        Assert.Equal("trace-1", error.TraceId);
    }

    [Fact]
    public async Task Html_error_is_presented_without_json_failure()
    {
        var handler = new StubHandler(_ => new(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("<html>gateway error</html>", Encoding.UTF8, "text/html")
        });
        var error = await Assert.ThrowsAsync<ApiException>(() => Client(handler).GetOrderAsync(Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.BadGateway, error.Status);
    }

    [Fact]
    public async Task Attempts_are_filtered_by_integration_and_paged()
    {
        var id = Guid.NewGuid();
        var handler = new StubHandler(request =>
        {
            Assert.Contains($"OrderIntegrationId={id}&PageNumber=3&PageSize=5", request.RequestUri!.Query);
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new PageResult<Attempt>([], 0, 3, 5, 0, false, false)) };
        });
        await Client(handler).GetAttemptsAsync(id, 3);
    }

    private static OrderBridgeApiClient Client(StubHandler handler)
        => new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") }, new HttpClient(handler) { BaseAddress = new Uri("http://integrations/") }, NullLogger<OrderBridgeApiClient>.Instance);

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpContent? Body { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content is not null)
                Body = new StringContent(await request.Content.ReadAsStringAsync(cancellationToken), Encoding.UTF8, "application/json");
            return respond(request);
        }
    }
}
