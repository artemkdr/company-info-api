using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CompanyInfo.Api.Shared.Filters;

/// <summary>
/// Action filter that handles response format negotiation.
/// Supports <c>?format=json</c> or <c>?format=xml</c> query parameter,
/// and falls back to the <c>Accept</c> header for content negotiation.
/// Default format is JSON.
/// </summary>
public class ResponseFormatFilter : IActionFilter, IResultFilter, IOrderedFilter
{
    /// <summary>
    /// Runs late in the filter pipeline so explicit format preference wins over metadata filters.
    /// </summary>
    public int Order => int.MaxValue;

    /// <summary>
    /// Before action execution: no-op.
    /// </summary>
    public void OnActionExecuting(ActionExecutingContext context) { }

    /// <summary>
    /// After action execution: no-op.
    /// </summary>
    public void OnActionExecuted(ActionExecutedContext context) { }

    /// <summary>
    /// Before result execution: sets the response content type based on the
    /// <c>format</c> query parameter or <c>Accept</c> header.
    /// </summary>
    /// <param name="context">The result executing context.</param>
    public void OnResultExecuting(ResultExecutingContext context)
    {
        var format = context
            .HttpContext.Request.Query["format"]
            .FirstOrDefault()
            ?.ToLowerInvariant();

        var acceptHeader = context.HttpContext.Request.Headers.Accept.ToString().ToLowerInvariant();

        if (context.Result is ObjectResult objectResult)
        {
            if (format == "xml")
            {
                context.HttpContext.Request.Headers.Accept = "application/xml";
                context.HttpContext.Response.ContentType = "application/xml";
                objectResult.ContentTypes.Clear();
                objectResult.ContentTypes.Add("application/xml");
            }
            else if (format == "json")
            {
                context.HttpContext.Request.Headers.Accept = "application/json";
                context.HttpContext.Response.ContentType = "application/json";
                objectResult.ContentTypes.Clear();
                objectResult.ContentTypes.Add("application/json");
            }
            else if (acceptHeader.Contains("application/xml") || acceptHeader.Contains("text/xml"))
            {
                context.HttpContext.Response.ContentType = "application/xml";
                objectResult.ContentTypes.Clear();
                objectResult.ContentTypes.Add("application/xml");
            }
            // If no explicit XML preference is provided, let default content negotiation return JSON.
        }
    }

    /// <summary>
    /// After result execution: no-op.
    /// </summary>
    public void OnResultExecuted(ResultExecutedContext context) { }
}
