using System.Xml.Serialization;

namespace CompanyInfo.Api.Application.Features.TvaCalculator;

/// <summary>
/// Response model for the TVA number calculation.
/// </summary>
[XmlRoot("TvaCalculation")]
public class CalculateTvaResponse
{
    /// <summary>
    /// The SIREN number used for calculation.
    /// </summary>
    [XmlElement("siren")]
    public string Siren { get; set; } = string.Empty;

    /// <summary>
    /// The calculated French TVA (VAT) number.
    /// </summary>
    [XmlElement("tvaNumber")]
    public string? TvaNumber { get; set; }

    /// <summary>
    /// Whether the provided SIREN was valid for TVA calculation.
    /// </summary>
    [XmlElement("isValid")]
    public bool IsValid { get; set; }

    /// <summary>
    /// Reason the SIREN was rejected, or <see langword="null"/> if the calculation succeeded.
    /// </summary>
    /// <remarks>
    /// HTTP status is always 200 OK. A non-<see langword="null"/> value means <see cref="IsValid"/> is
    /// <see langword="false"/> and the value describes why the SIREN could not be processed.
    /// </remarks>
    [XmlElement("errorMessage")]
    public string? ErrorMessage { get; set; }
}
