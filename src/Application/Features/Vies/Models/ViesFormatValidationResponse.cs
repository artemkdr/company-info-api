using System.Xml.Serialization;

namespace CompanyInfo.Api.Application.Features.Vies;

/// <summary>
/// Response model for VIES VAT number format validation (local check only, no external API).
/// </summary>
[XmlRoot("ViesFormatValidation")]
public class ViesFormatValidationResponse
{
    /// <summary>
    /// The VAT number that was validated.
    /// </summary>
    [XmlElement("vatNumber")]
    public string VatNumber { get; set; } = string.Empty;

    /// <summary>
    /// Whether the VAT number format is structurally valid for its country prefix.
    /// This check is performed locally and does not call any external service.
    /// </summary>
    [XmlElement("isValid")]
    public bool IsValid { get; set; }
}
