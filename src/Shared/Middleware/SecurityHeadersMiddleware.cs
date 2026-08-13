namespace CompanyInfo.Api.Shared.Middleware;

/// <summary>
/// Middleware that adds security and cache-control headers to all HTTP responses.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecurityHeadersMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// Invokes the middleware, adding security headers to the response.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        // Security headers
        context.Response.OnStarting(() =>
        {
            // Prevent MIME-type sniffing
            if (!context.Response.Headers.ContainsKey("X-Content-Type-Options"))
            {
                context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            }

            // Cross-Origin-Resource-Policy: restrict sharing to same origin
            if (!context.Response.Headers.ContainsKey("Cross-Origin-Resource-Policy"))
            {
                context.Response.Headers["Cross-Origin-Resource-Policy"] = "same-origin";
            }

            // Cross-Origin-Embedder-Policy: require CORP for cross-origin resources
            if (!context.Response.Headers.ContainsKey("Cross-Origin-Embedder-Policy"))
            {
                context.Response.Headers["Cross-Origin-Embedder-Policy"] = "require-corp";
            }

            // Cross-Origin-Opener-Policy: isolate browsing context
            if (!context.Response.Headers.ContainsKey("Cross-Origin-Opener-Policy"))
            {
                context.Response.Headers["Cross-Origin-Opener-Policy"] = "same-origin";
            }

            // Cache-Control: prevent proxy caching of API responses
            if (
                !context.Response.Headers.ContainsKey("Cache-Control")
                && context.Request.Path.StartsWithSegments("/api")
            )
            {
                context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
                context.Response.Headers["Pragma"] = "no-cache";
                context.Response.Headers["Expires"] = "0";
            }

            return Task.CompletedTask;
        });

        await _next(context);
    }
}
