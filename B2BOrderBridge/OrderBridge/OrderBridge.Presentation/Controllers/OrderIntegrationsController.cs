using MediatR;
using Microsoft.AspNetCore.Mvc;
using OrderBridge.Application.Features.OrderIntegrations.Models;
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

    [HttpGet("attempts")]
    [ProducesResponseType(
        typeof(PaginationResult<IntegrationAttempt>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaginationResult<IntegrationAttempt>>> GetAttemptsAsync(
        [FromQuery] GetPagedIntegrationAttemptsRequest request,
        CancellationToken cancellationToken)
    {
        var query = new GetPagedIntegrationAttemptsQuery(
            new PaginationRequest(request.PageNumber, request.PageSize),
            new IntegrationAttemptFilter(
                request.Id,
                request.OrderIntegrationId,
                request.AttemptNumberFrom,
                request.AttemptNumberTo,
                request.Outcome,
                request.ResponseStatusCode,
                request.ErrorCodeSearchTerm,
                request.ErrorMessageSearchTerm,
                request.TraceIdSearchTerm,
                request.StartedAtFrom,
                request.StartedAtTo,
                request.CompletedAtFrom,
                request.CompletedAtTo,
                request.SearchTerm),
            request.SortBy,
            request.SortDirection);

        var result = await sender.Send(query, cancellationToken);

        return FromPaginationResult(result);
    }

    [HttpGet("attempts/{id:guid}")]
    [ProducesResponseType(typeof(IntegrationAttempt), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IntegrationAttempt>> GetAttemptByIdAsync(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetIntegrationAttemptByIdQuery(id),
            cancellationToken);

        return FromResult(result);
    }
}
