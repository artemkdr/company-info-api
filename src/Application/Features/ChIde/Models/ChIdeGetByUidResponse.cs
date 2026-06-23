using System.Xml.Serialization;
using CompanyInfo.Api.Shared.Contracts;

namespace CompanyInfo.Api.Application.Features.ChIde;

/// <summary>
/// Response model for CH IDE UID lookup.
/// </summary>
[XmlRoot("ChIdeCompany")]
public class ChIdeGetByUidResponse : IExternalValidationResponse
{
    /// <summary>
    /// The UID number (e.g., CHE-123.456.789).
    /// </summary>
    [XmlElement("uid")]
    public string Uid { get; set; } = string.Empty;

    /// <summary>
    /// The organisation name.
    /// </summary>
    [XmlElement("organisationName")]
    public string? OrganisationName { get; set; }

    /// <summary>
    /// The street address.
    /// </summary>
    [XmlElement("street")]
    public string? Street { get; set; }

    /// <summary>
    /// The city.
    /// </summary>
    [XmlElement("city")]
    public string? City { get; set; }

    /// <summary>
    /// The postal code / ZIP code.
    /// </summary>
    [XmlElement("zipCode")]
    public string? ZipCode { get; set; }

    /// <summary>
    /// The canton abbreviation (e.g., GE, VD, ZH).
    /// </summary>
    [XmlElement("canton")]
    public string? Canton { get; set; }

    /// <summary>
    /// The legal form of the organisation.
    /// </summary>
    [XmlElement("legalForm")]
    public string? LegalForm { get; set; }

    /// <summary>
    /// The detailed IDE register status code (<c>uidregStatusEnterpriseDetail</c>).
    /// 1 = provisional, 2 = reactivation in progress, 3 = definitive,
    /// 4 = mutation in progress, 5 = deleted, 6 = permanently deleted, 7 = cancelled.
    /// </summary>
    [XmlElement("status")]
    public string? Status { get; set; }

    /// <summary>
    /// Whether the organisation is active in the IDE register.
    /// Active means <see cref="Status"/> is 3 (definitive) or 4 (mutation in progress).
    /// </summary>
    [XmlElement("isActive")]
    public bool IsActive { get; set; }

    /// <summary>
    /// Whether the organisation's data is publicly accessible on the internet
    /// (<c>uidregPublicStatus</c> = 1). False means access is blocked for the public.
    /// </summary>
    [XmlElement("isPublic")]
    public bool IsPublic { get; set; }

    /// <summary>
    /// Whether the lookup was successful.
    /// </summary>
    [XmlElement("found")]
    public bool Found { get; set; }

    /// <summary>
    /// Error message returned by the CH IDE external service, or <see langword="null"/> if the lookup succeeded.
    /// </summary>
    /// <remarks>
    /// HTTP status is always 200 OK. A non-<see langword="null"/> value means the external service
    /// rejected the request or returned an error; callers should treat the result as invalid.
    /// </remarks>
    [XmlElement("errorMessage")]
    public string? ErrorMessage { get; set; }
}
