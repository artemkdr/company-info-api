using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using System.Xml.Serialization;

namespace CompanyInfo.Api.Shared.Middleware;

/// <summary>
/// Middleware for global exception handling.
/// Catches unhandled exceptions from controllers and returns appropriate HTTP responses.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger
    )
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        _logger.LogError(exception, "An error occurred processing request");

        var (statusCode, message) = exception switch
        {
            ArgumentException => (StatusCodes.Status400BadRequest, exception.Message),
            FormatException => (StatusCodes.Status400BadRequest, exception.Message),
            ValidationException => (StatusCodes.Status400BadRequest, exception.Message),
            HttpRequestException httpEx => (
                httpEx.StatusCode is not null
                    ? (int)httpEx.StatusCode.Value
                    : StatusCodes.Status502BadGateway,
                httpEx.StatusCode is not null ? httpEx.Message : "Upstream request failed."
            ),
            FluentValidation.ValidationException => (
                StatusCodes.Status400BadRequest,
                exception.Message
            ),
            _ => (StatusCodes.Status500InternalServerError, "Internal server error"),
        };

        context.Response.StatusCode = statusCode;
        var contentType = ResolveContentType(context);
        context.Response.ContentType = contentType;

        var errorResponse = new ErrorResponse { StatusCode = statusCode, Message = message };

        var responseBody =
            contentType == "application/xml"
                ? SerializeToXml(errorResponse)
                : JsonSerializer.Serialize(errorResponse, JsonOptions);

        await context.Response.WriteAsync(responseBody);
    }

    private static string ResolveContentType(HttpContext context)
    {
        var format = context.Request.Query["format"].FirstOrDefault()?.ToLowerInvariant();

        if (format == "xml")
        {
            return "application/xml";
        }

        if (format == "json")
        {
            return "application/json";
        }

        var acceptHeader = context.Request.Headers.Accept.ToString();
        if (
            acceptHeader.Contains("application/xml", StringComparison.OrdinalIgnoreCase)
            || acceptHeader.Contains("text/xml", StringComparison.OrdinalIgnoreCase)
        )
        {
            return "application/xml";
        }

        return "application/json";
    }

    private static string SerializeToXml(ErrorResponse response)
    {
        var serializer = new XmlSerializer(typeof(ErrorResponse));
        using var stringWriter = new Utf8StringWriter();
        serializer.Serialize(stringWriter, response);
        return stringWriter.ToString();
    }

    [XmlRoot("error")]
    public class ErrorResponse
    {
        [XmlElement("statusCode")]
        public int StatusCode { get; set; }

        [XmlElement("message")]
        public string Message { get; set; } = string.Empty;
    }

    private sealed class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
    }
}
