using MediatR;
using Microsoft.AspNetCore.Mvc;
using OrderBridge.Orders.Application.Features.Orders.Commands;
using OrderBridge.Orders.Application.Features.Orders.Queries;
using OrderBridge.Orders.Application.Features.Orders.ValueObjects;
using OrderBridge.Orders.Api.Requests;
using Shared.Application.Pagination;

namespace OrderBridge.Orders.Api.Controllers;

[Route("api/orders")]
public sealed class OrderController(ISender sender) : BaseApiController
{
    [HttpPost]
    [ProducesResponseType(typeof(CreateOrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreateOrderResponse>> CreateAsync(
        [FromBody] CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateOrderCommand(
            request.SourceSystem,
            request.ExternalOrderId,
            request.Customer,
            request.Currency,
            request.Lines);

        var result = await sender.Send(command, cancellationToken);

        return FromCreatedResult(
            result,
            nameof(GetByIdAsync),
            response => new { id = response.Id });
    }

    [HttpGet("{id:guid}", Name = nameof(GetByIdAsync))]
    [ProducesResponseType(typeof(OrderSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderSummaryDto>> GetByIdAsync(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var query = new GetOrderByIdQuery(id);

        var result = await sender.Send(query, cancellationToken);

        return FromResult(result);
    }

    [HttpGet("{id:guid}/details")]
    [ProducesResponseType(typeof(OrderDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDetailsDto>> GetDetailsByIdAsync(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetOrderDetailsByIdQuery(id),
            cancellationToken);

        return FromResult(result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(PaginationResult<OrderSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaginationResult<OrderSummaryDto>>> GetPagedAsync(
        [FromQuery] GetPagedOrdersRequest request,
        CancellationToken cancellationToken)
    {
        var pagination = new PaginationRequest(request.PageNumber, request.PageSize);
        var filter = new OrdersFilter(
            request.Id,
            request.SourceSystem,
            request.ExternalOrderId,
            request.CustomerName,
            request.OrderStatus,
            request.PaymentStatus,
            request.TotalAmountFrom,
            request.TotalAmountTo,
            request.Currency,
            request.CreatedAtFrom,
            request.CreatedAtTo,
            request.SearchTerm);
        var query = new GetPagedOrdersQuery(
            pagination,
            filter,
            request.SortBy,
            request.SortDirection);

        var result = await sender.Send(query, cancellationToken);

        return FromPaginationResult(result);
    }
}
