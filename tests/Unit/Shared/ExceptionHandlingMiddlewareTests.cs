using System.Text.Json;
using CompanyInfo.Api.Shared.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace CompanyInfo.Api.Tests.Unit.Shared;

public class ExceptionHandlingMiddlewareTests
{
    [Fact(DisplayName = "Should return JSON error response when format=json")]
    public async Task InvokeAsync_FormatJson_ReturnsJsonErrorResponse()
    {
        var context = CreateHttpContext("?format=json");
        var middleware = CreateMiddleware(new ArgumentException("Invalid input"));

        await middleware.InvokeAsync(context);

        var body = await ReadResponseBodyAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        context.Response.ContentType.Should().Be("application/json");

        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("statusCode")
            .GetInt32()
            .Should()
            .Be(StatusCodes.Status400BadRequest);
        json.RootElement.GetProperty("message").GetString().Should().Be("Invalid input");
    }

    [Fact(DisplayName = "Should return XML error response when format=xml")]
    public async Task InvokeAsync_FormatXml_ReturnsXmlErrorResponse()
    {
        var context = CreateHttpContext("?format=xml");
        var middleware = CreateMiddleware(new ArgumentException("Invalid input"));

        await middleware.InvokeAsync(context);

        var body = await ReadResponseBodyAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        context.Response.ContentType.Should().Be("application/xml");
        body.Should().Contain("<statusCode>400</statusCode>");
        body.Should().Contain("<message>Invalid input</message>");
    }

    [Fact(DisplayName = "Should use Accept header fallback when format is not provided")]
    public async Task InvokeAsync_AcceptHeaderXml_ReturnsXmlErrorResponse()
    {
        var context = CreateHttpContext();
        context.Request.Headers.Accept = "application/xml";
        var middleware = CreateMiddleware(new ArgumentException("Invalid input"));

        await middleware.InvokeAsync(context);

        context.Response.ContentType.Should().Be("application/xml");
    }

    [Fact(DisplayName = "Should default to JSON response when no format hint is provided")]
    public async Task InvokeAsync_NoFormatHint_ReturnsJsonErrorResponse()
    {
        var context = CreateHttpContext();
        var middleware = CreateMiddleware(new ArgumentException("Invalid input"));

        await middleware.InvokeAsync(context);

        context.Response.ContentType.Should().Be("application/json");
    }

    [Fact(DisplayName = "Should not leak internal error message for 500 responses")]
    public async Task InvokeAsync_InternalError_ReturnsGenericMessage()
    {
        var context = CreateHttpContext("?format=json");
        var middleware = CreateMiddleware(new InvalidOperationException("Sensitive message"));

        await middleware.InvokeAsync(context);

        var body = await ReadResponseBodyAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("message").GetString().Should().Be("Internal server error");
    }

    private static ExceptionHandlingMiddleware CreateMiddleware(Exception exception)
    {
        RequestDelegate next = _ => throw exception;
        var logger = Substitute.For<ILogger<ExceptionHandlingMiddleware>>();
        return new ExceptionHandlingMiddleware(next, logger);
    }

    private static DefaultHttpContext CreateHttpContext(string? queryString = null)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        if (!string.IsNullOrWhiteSpace(queryString))
        {
            context.Request.QueryString = new QueryString(queryString);
        }

        return context;
    }

    private static async Task<string> ReadResponseBodyAsync(DefaultHttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        return await reader.ReadToEndAsync();
    }
}
