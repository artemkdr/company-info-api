using System.Xml.Serialization;

namespace CompanyInfo.Api.Application.Features.Health;

/// <summary>
/// Response model for the health check endpoint.
/// </summary>
[XmlRoot("Health")]
public class HealthResponse
{
    /// <summary>
    /// Overall health status. Always <c>"Healthy"</c> when the API is running.
    /// </summary>
    [XmlElement("status")]
    public string Status { get; set; } = "Healthy";

    /// <summary>
    /// UTC timestamp of when the health check was performed.
    /// </summary>
    [XmlElement("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// API version reported by the health check.
    /// </summary>
    [XmlElement("version")]
    public string Version { get; set; } = string.Empty;
}
