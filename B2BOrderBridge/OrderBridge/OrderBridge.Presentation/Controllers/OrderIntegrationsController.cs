using MediatR;
using Microsoft.AspNetCore.Mvc;
using OrderBridge.Application.Features.OrderIntegrations.Queries;
using OrderBridge.Application.Features.OrderIntegrations.ValueObjects;
using OrderBridge.Presentation.Requests;
using Shared.Application.Pagination;

namespace OrderBridge.Presentation.Controllers;

[Route("api/order-integrations")]
public sealed class OrderIntegrationsController(ISender sender) : BaseApiController
{
    [HttpGet]
    [ProducesResponseType(
        typeof(PaginationResult<OrderIntegrationDto>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaginationResult<OrderIntegrationDto>>> GetPagedAsync(
        [FromQuery] GetPagedOrderIntegrationsRequest request,
        CancellationToken cancellationToken)
    {
        var query = new GetPagedOrderIntegrationsQuery(
            new PaginationRequest(request.PageNumber, request.PageSize),
            new OrderIntegrationsFilter(
                request.Id,
                request.OrderId,
                request.IntegrationTarget,
                request.IntegrationOperation,
                request.IntegrationStatus,
                request.AttemptCountFrom,
                request.AttemptCountTo,
                request.CreatedAtFrom,
                request.CreatedAtTo,
                request.NextAttemptAtTo,
                request.SearchTerm),
            request.SortBy,
            request.SortDirection);

        var result = await sender.Send(query, cancellationToken);

        return FromPaginationResult(result);
    }
}
