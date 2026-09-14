using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using OrderBridge.Presentation.Controllers;
using Shared.Application.Pagination;
using Shared.Application.Results;
using Xunit;

namespace OrderBridge.Presentation.Tests;

public sealed class BaseApiControllerTests
{
    [Theory]
    [InlineData(ErrorType.Validation, 400)]
    [InlineData(ErrorType.NotFound, 404)]
    [InlineData(ErrorType.Conflict, 409)]
    [InlineData(ErrorType.Unauthorized, 401)]
    [InlineData(ErrorType.Forbidden, 403)]
    [InlineData(ErrorType.Failure, 500)]
    public void Errors_are_problem_details(ErrorType type, int status)
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var controller = Create(services);
        var response = Assert.IsType<ObjectResult>(controller.Value(Result<int>.Failure(new("test.error", "Description", type))).Result);
        var body = Assert.IsType<ProblemDetails>(response.Value);
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(status, body.Status);
        Assert.Contains("application/problem+json", response.ContentTypes);
        Assert.Equal("/api/orders", body.Instance);
        Assert.True(body.Extensions.ContainsKey("traceId"));
        Assert.Equal(status == 500 ? "server.error" : "test.error", body.Extensions["code"]);
        Assert.Equal(status == 500 ? "An unexpected error occurred." : "Description", body.Detail);
    }

    [Fact]
    public void Success_returns_value_or_no_content()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var controller = Create(services);
        Assert.IsType<NoContentResult>(controller.WithoutBody(Result.Success()));
        Assert.Equal(42, Assert.IsType<OkObjectResult>(controller.Value(Result<int>.Success(42)).Result).Value);
    }

    [Fact]
    public void Empty_page_preserves_metadata()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var page = new PaginationResult<int>([], 0, new());
        var response = Create(services).Page(Result<PaginationResult<int>>.Success(page));
        Assert.Same(page, Assert.IsType<OkObjectResult>(response.Result).Value);
    }

    [Fact]
    public void Creation_returns_route_and_does_not_evaluate_callback_on_error()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var controller = Create(services);
        var response = Assert.IsType<CreatedAtRouteResult>(
            controller.Created(Result<int>.Success(42), id => new { id }).Result);
        Assert.Equal(201, response.StatusCode);
        Assert.Equal("GetOrder", response.RouteName);
        Assert.Equal(42, response.RouteValues!["id"]);
        var error = Result<int>.Failure(new("conflict", "Already exists.", ErrorType.Conflict));
        Assert.Equal(409, Assert.IsType<ObjectResult>(
            controller.Created(error, _ => throw new Exception("Must not run")).Result).StatusCode);
    }

    [Fact]
    public void Accepted_returns_tracking_route()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var response = Assert.IsType<AcceptedAtRouteResult>(Create(services).Accepted(Result<int>.Success(42)).Result);
        Assert.Equal(202, response.StatusCode);
        Assert.Equal(42, response.RouteValues!["id"]);
    }

    private static TestController Create(IServiceProvider services) => new()
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestServices = services, Request = { Path = "/api/orders" } }
        }
    };

    private sealed class TestController : BaseApiController
    {
        public ActionResult WithoutBody(Result result) => FromResult(result);
        public ActionResult<int> Value(Result<int> result) => FromResult(result);
        public ActionResult<PaginationResult<int>> Page(Result<PaginationResult<int>> result) => FromPaginationResult(result);
        public ActionResult<int> Created(Result<int> result, Func<int, object> route) => FromCreatedResult(result, "GetOrder", route);
        public ActionResult<int> Accepted(Result<int> result) => FromAcceptedResult(result, "GetOrder", id => new { id });
    }
}

