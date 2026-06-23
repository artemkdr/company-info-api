using System.Xml.Serialization;
using CompanyInfo.Api.Shared.Contracts;

namespace CompanyInfo.Api.Application.Features.Insee;

/// <summary>
/// Response model for INSEE establishment (SIRET) lookup.
/// </summary>
[XmlRoot("InseeEstablishment")]
public class InseeEstablishmentResponse : IExternalValidationResponse
{
    /// <summary>
    /// The SIRET number (14 digits).
    /// </summary>
    [XmlElement("siret")]
    public string Siret { get; set; } = string.Empty;

    /// <summary>
    /// The SIREN number (first 9 digits of SIRET).
    /// </summary>
    [XmlElement("siren")]
    public string? Siren { get; set; }

    /// <summary>
    /// The legal unit denomination (company name).
    /// </summary>
    [XmlElement("companyName")]
    public string? CompanyName { get; set; }

    /// <summary>
    /// The trade name / enseigne of the establishment.
    /// </summary>
    [XmlElement("tradeName")]
    public string? TradeName { get; set; }

    /// <summary>
    /// The principal activity code (APE/NAF code).
    /// </summary>
    [XmlElement("activityCode")]
    public string? ActivityCode { get; set; }

    /// <summary>
    /// The last name (for individual enterprises).
    /// </summary>
    [XmlElement("lastName")]
    public string? LastName { get; set; }

    /// <summary>
    /// The first name (for individual enterprises).
    /// </summary>
    [XmlElement("firstName")]
    public string? FirstName { get; set; }

    /// <summary>
    /// The postal code of the establishment.
    /// </summary>
    [XmlElement("postalCode")]
    public string? PostalCode { get; set; }

    /// <summary>
    /// The city of the establishment.
    /// </summary>
    [XmlElement("city")]
    public string? City { get; set; }

    /// <summary>
    /// The Lambert coordinates of the establishment.
    /// </summary>
    [XmlElement("lambertAbscis")]
    public double? LambertAbscis { get; set; }

    /// <summary>
    /// The Lambert coordinates of the establishment.
    /// </summary>
    [XmlElement("lambertOrdonnee")]
    public double? LambertOrdonnee { get; set; }

    /// <summary>
    /// Whether the legal unit is administratively active.
    /// </summary>
    [XmlElement("isActive")]
    public bool IsActive { get; set; }

    /// <summary>
    /// Whether the company's principal activity is in the automotive sector (APE starts with "45").
    /// </summary>
    [XmlElement("isAutomotive")]
    public bool IsAutomotive { get; set; }

    /// <summary>
    /// Whether the lookup was successful.
    /// </summary>
    [XmlElement("found")]
    public bool Found { get; set; }

    /// <summary>
    /// Error message returned by the INSEE external service, or <see langword="null"/> if the lookup succeeded.
    /// </summary>
    /// <remarks>
    /// HTTP status is always 200 OK. A non-<see langword="null"/> value means the external service
    /// rejected the request or returned an error; callers should treat the result as invalid.
    /// </remarks>
    [XmlElement("errorMessage")]
    public string? ErrorMessage { get; set; }
}
