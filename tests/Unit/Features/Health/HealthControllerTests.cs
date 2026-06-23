using CompanyInfo.Api.Application.Features.Health;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace CompanyInfo.Api.Tests.Unit.Features.Health;

/// <summary>
/// Unit tests for <see cref="HealthController"/>.
/// </summary>
public class HealthControllerTests
{
    private static HealthController CreateController(string? apiVersion = null)
    {
        var httpContext = new DefaultHttpContext();

        if (apiVersion != null)
        {
            // Asp.Versioning reads the requested version from route values
            httpContext.Request.RouteValues["version"] = apiVersion;
        }

        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ControllerActionDescriptor()
        );
        var controller = new HealthController
        {
            ControllerContext = new ControllerContext(actionContext),
        };

        return controller;
    }

    [Fact(DisplayName = "GET /health should return 200 OK")]
    public void Get_ReturnsOk()
    {
        var controller = CreateController();

        var result = controller.Get();

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact(DisplayName = "GET /health should return HealthResponse with Healthy status")]
    public void Get_ReturnsHealthyStatus()
    {
        var controller = CreateController();

        var result = (OkObjectResult)controller.Get();
        var response = result.Value as HealthResponse;

        response.Should().NotBeNull();
        response!.Status.Should().Be("Healthy");
    }

    [Fact(DisplayName = "GET /health should return a recent UTC timestamp")]
    public void Get_ReturnsRecentTimestamp()
    {
        var before = DateTime.UtcNow;
        var controller = CreateController();

        var result = (OkObjectResult)controller.Get();
        var response = result.Value as HealthResponse;
        var after = DateTime.UtcNow;

        response.Should().NotBeNull();
        response!.Timestamp.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact(
        DisplayName = "GET /health should fall back to version \"1.0\" when no API version in context"
    )]
    public void Get_NoApiVersion_FallsBackToDefault()
    {
        var controller = CreateController();

        var result = (OkObjectResult)controller.Get();
        var response = result.Value as HealthResponse;

        response.Should().NotBeNull();
        response!.Version.Should().Be("1.0");
    }
}
