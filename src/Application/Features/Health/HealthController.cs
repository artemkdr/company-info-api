using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CompanyInfo.Api.Application.Features.Health;

/// <summary>
/// Controller for the API health check endpoint.
/// This endpoint is intentionally unauthenticated so that load balancers,
/// uptime monitors, and orchestrators can probe liveness without an API key.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/health")]
[AllowAnonymous]
public class HealthController : ControllerBase
{
    /// <summary>
    /// Returns the health status of the API.
    /// </summary>
    /// <returns>A <see cref="HealthResponse"/> indicating the API is running.</returns>
    /// <response code="200">The API is healthy.</response>
    [HttpGet]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    public IActionResult Get()
    {
        var apiVersion = HttpContext.GetRouteValue("version")?.ToString() ?? "1.0";

        var response = new HealthResponse
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Version = apiVersion,
        };

        return Ok(response);
    }
}
