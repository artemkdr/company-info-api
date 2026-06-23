using System.Xml.Serialization;
using CompanyInfo.Api.Shared.Contracts;

namespace CompanyInfo.Api.Application.Features.Vies;

/// <summary>
/// Response model for the VIES active-check endpoint.
/// Indicates whether a VAT number is currently registered and active in the VIES system.
/// This involves a call to the external EU VIES service and may fail or be retried.
/// </summary>
[XmlRoot("ViesActiveCheck")]
public class ViesValidationResponse : IExternalValidationResponse
{
    /// <summary>
    /// The VAT number that was checked.
    /// </summary>
    [XmlElement("vatNumber")]
    public string VatNumber { get; set; } = string.Empty;

    /// <summary>
    /// Whether the VAT number is currently active (registered and in use) in the VIES system.
    /// </summary>
    [XmlElement("isActive")]
    public bool IsActive { get; set; }

    /// <summary>
    /// Error message returned by the external EU VIES service, or <see langword="null"/> if the check succeeded.
    /// </summary>
    /// <remarks>
    /// HTTP status is always 200 OK. A non-<see langword="null"/> value means the external service
    /// rejected the request or returned an error; callers should treat the result as invalid.
    /// </remarks>
    [XmlElement("errorMessage")]
    public string? ErrorMessage { get; set; }
}
