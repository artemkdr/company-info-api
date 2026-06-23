using CompanyInfo.Api.Shared.Filters;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace CompanyInfo.Api.Tests.Unit.Shared;

/// <summary>
/// Unit tests for the ResponseFormatFilter.
/// </summary>
public class ResponseFormatFilterTests
{
    private readonly ResponseFormatFilter _filter = new();

    private static ResultExecutingContext CreateContext(
        string? formatQueryParam,
        object? resultValue = null
    )
    {
        var httpContext = new DefaultHttpContext();
        if (formatQueryParam != null)
        {
            httpContext.Request.QueryString = new QueryString($"?format={formatQueryParam}");
        }

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());

        var objectResult = new ObjectResult(resultValue ?? new { test = true });

        return new ResultExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            objectResult,
            new object()
        );
    }

    [Fact(DisplayName = "Should set content type to XML when format=xml")]
    public void OnResultExecuting_FormatXml_SetsXmlContentType()
    {
        var context = CreateContext("xml");

        _filter.OnResultExecuting(context);

        var objectResult = context.Result as ObjectResult;
        objectResult.Should().NotBeNull();
        objectResult!.ContentTypes.Should().Contain("application/xml");
        objectResult.ContentTypes.Should().NotContain("application/json");
    }

    [Fact(DisplayName = "Should set content type to JSON when format=json")]
    public void OnResultExecuting_FormatJson_SetsJsonContentType()
    {
        var context = CreateContext("json");

        _filter.OnResultExecuting(context);

        var objectResult = context.Result as ObjectResult;
        objectResult.Should().NotBeNull();
        objectResult!.ContentTypes.Should().Contain("application/json");
        objectResult.ContentTypes.Should().NotContain("application/xml");
    }

    [Fact(DisplayName = "Should not modify content types when no format query parameter")]
    public void OnResultExecuting_NoFormat_DoesNotModifyContentTypes()
    {
        var context = CreateContext(null);

        _filter.OnResultExecuting(context);

        var objectResult = context.Result as ObjectResult;
        objectResult.Should().NotBeNull();
        objectResult!.ContentTypes.Should().BeEmpty();
    }

    [Fact(DisplayName = "Should handle format parameter case-insensitively")]
    public void OnResultExecuting_UppercaseXml_SetsXmlContentType()
    {
        var context = CreateContext("XML");

        _filter.OnResultExecuting(context);

        var objectResult = context.Result as ObjectResult;
        objectResult.Should().NotBeNull();
        objectResult!.ContentTypes.Should().Contain("application/xml");
    }
}
