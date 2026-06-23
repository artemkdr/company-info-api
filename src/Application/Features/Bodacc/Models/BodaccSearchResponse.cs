using System.Xml.Serialization;
using CompanyInfo.Api.Shared.Contracts;

namespace CompanyInfo.Api.Application.Features.Bodacc;

/// <summary>
/// Response model for BODACC search results.
/// </summary>
[XmlRoot("BodaccSearch")]
public class BodaccSearchResponse : IExternalValidationResponse
{
    /// <summary>
    /// The registration number searched (SIREN or SIRET).
    /// </summary>
    [XmlElement("registrationNumber")]
    public string RegistrationNumber { get; set; } = string.Empty;

    /// <summary>
    /// Whether the company appears to be in liquidation or insolvency proceedings based on BODACC records.
    /// Detected via <c>familleavis = "collective"</c> or liquidation-related keywords in judgment data.
    /// </summary>
    [XmlElement("isInLiquidation")]
    public bool IsInLiquidation { get; set; }

    /// <summary>
    /// Whether the company has been struck off the trade register (<c>familleavis = "radiation"</c>).
    /// </summary>
    [XmlElement("isRadiated")]
    public bool IsRadiated { get; set; }

    /// <summary>
    /// The total number of BODACC records found.
    /// </summary>
    [XmlElement("totalRecords")]
    public int TotalRecords { get; set; }

    /// <summary>
    /// The BODACC publication records found.
    /// </summary>
    [XmlArray("records")]
    [XmlArrayItem("record")]
    public List<BodaccRecord> Records { get; set; } = new();

    /// <summary>
    /// Error message returned by the BODACC external service, or <see langword="null"/> if the search succeeded.
    /// </summary>
    /// <remarks>
    /// HTTP status is always 200 OK. A non-<see langword="null"/> value means the external service
    /// rejected the request or returned an error; callers should treat the result as invalid.
    /// </remarks>
    [XmlElement("errorMessage")]
    public string? ErrorMessage { get; set; }
}
