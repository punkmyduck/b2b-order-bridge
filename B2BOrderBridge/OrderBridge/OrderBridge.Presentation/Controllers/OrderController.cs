using MediatR;
using Microsoft.AspNetCore.Mvc;
using OrderBridge.Application.Features.Orders.Commands;
using OrderBridge.Application.Features.Orders.Queries;
using OrderBridge.Application.Features.Orders.ValueObjects;
using OrderBridge.Presentation.Requests;

namespace OrderBridge.Presentation.Controllers;

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
}
