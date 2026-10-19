namespace OrderBridge.Web.Models;

// REST contracts belong to the HTTP client; the UI does not reference backend layers.
public sealed record Customer(string CompanyName, string TaxId, string ContactEmail);
public sealed record OrderLineInput(string Sku, string Name, decimal Quantity, decimal UnitPrice);
public sealed record CreateOrderInput(string SourceSystem, string ExternalOrderId, Customer Customer,
    string Currency, IReadOnlyList<OrderLineInput> Lines);
public sealed record CreatedOrder(Guid Id);
public sealed record OrderSummary(Guid Id, string SourceSystem, string ExternalOrderId, Customer Customer,
    string OrderStatus, string PaymentStatus, decimal TotalAmount, string Currency, DateTimeOffset CreatedAt);
public sealed record OrderDetails(Guid Id, string SourceSystem, string ExternalOrderId, Customer Customer,
    string OrderStatus, string PaymentStatus, decimal TotalAmount, string Currency, DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt, IReadOnlyList<OrderLine> Lines, IReadOnlyList<OrderIntegration> Integrations,
    bool IntegrationsAvailable = true);
public sealed record OrderLine(Guid Id, string Sku, string Name, decimal Quantity, decimal UnitPrice, decimal LineTotal);
public sealed record OrderIntegration(Guid Id, Guid OrderId, string IntegrationTarget, string IntegrationOperation,
    string IntegrationStatus, string? ExternalEntityId, string IdempotencyKey, int AttemptCount,
    DateTimeOffset? NextAttemptAt, string? LastErrorCode, string? LastErrorMessage, DateTimeOffset CreatedAt,
    DateTimeOffset? LastAttemptAt, DateTimeOffset? CompletedAt, DateTimeOffset? UpdatedAt);
public sealed record Attempt(Guid Id, Guid OrderIntegrationId, int AttemptNumber, string Outcome,
    int? ResponseStatusCode, string? ErrorCode, string? ErrorMessage, string? TraceId,
    DateTimeOffset StartedAt, DateTimeOffset? CompletedAt);
public sealed record PageResult<T>(IReadOnlyList<T> Items, long TotalCount, int PageNumber,
    int PageSize, long TotalPages, bool HasPreviousPage, bool HasNextPage);
