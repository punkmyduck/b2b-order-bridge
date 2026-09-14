using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Shared.Application.Pagination;
using Shared.Application.Results;

namespace OrderBridge.Presentation.Controllers;

[ApiController]
public abstract class BaseApiController : ControllerBase
{
    /// <summary>A successful command without a response body returns 204.</summary>
    protected ActionResult FromResult(Result result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.IsSuccess ? NoContent() : FromError(result.Error!);
    }

    /// <summary>Returns the value directly, without exposing the Result wrapper.</summary>
    protected ActionResult<T> FromResult<T>(Result<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.IsSuccess ? Ok(result.Value) : FromError(result.Error!);
    }

    /// <summary>Empty and out-of-range pages still return 200 with pagination metadata.</summary>
    protected ActionResult<PaginationResult<T>> FromPaginationResult<T>(Result<PaginationResult<T>> result)
        => FromResult(result);

    protected ActionResult<PaginationResult<T>> FromPaginationResult<T>(PaginationResult<T> page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return Ok(page);
    }

    /// <summary>The route must identify an existing GET endpoint for the created resource.</summary>
    protected ActionResult<T> FromCreatedResult<T>(
        Result<T> result, string routeName, Func<T, object> routeValues)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.IsFailure)
            return FromError(result.Error!);

        ArgumentException.ThrowIfNullOrWhiteSpace(routeName);
        ArgumentNullException.ThrowIfNull(routeValues);
        return CreatedAtRoute(routeName, routeValues(result.Value), result.Value);
    }

    /// <summary>202 means accepted for background processing, not completed.</summary>
    protected ActionResult<T> FromAcceptedResult<T>(
        Result<T> result, string routeName, Func<T, object> routeValues)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.IsFailure)
            return FromError(result.Error!);

        ArgumentException.ThrowIfNullOrWhiteSpace(routeName);
        ArgumentNullException.ThrowIfNull(routeValues);
        return AcceptedAtRoute(routeName, routeValues(result.Value), result.Value);
    }

    protected ObjectResult FromError(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        var status = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status500InternalServerError
        };

        if (status == StatusCodes.Status500InternalServerError)
            HttpContext.RequestServices.GetService<ILogger<BaseApiController>>()?
                .LogError("Application failure {ErrorCode}: {ErrorDescription}", error.Code, error.Description);

        var problem = new ProblemDetails
        {
            Status = status,
            Type = "about:blank",
            Title = ReasonPhrases.GetReasonPhrase(status),
            Detail = status == StatusCodes.Status500InternalServerError
                ? "An unexpected error occurred." : error.Description,
            Instance = Request.Path
        };
        problem.Extensions["code"] = status == StatusCodes.Status500InternalServerError
            ? "server.error" : error.Code;
        problem.Extensions["traceId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier;

        var response = new ObjectResult(problem) { StatusCode = status };
        response.ContentTypes.Add("application/problem+json");
        return response;
    }
}
