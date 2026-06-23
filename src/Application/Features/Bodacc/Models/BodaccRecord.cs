using System.Xml.Serialization;

namespace CompanyInfo.Api.Application.Features.Bodacc;

/// <summary>
/// A single BODACC publication record.
/// </summary>
[XmlRoot("BodaccRecord")]
public class BodaccRecord
{
    /// <summary>
    /// The publication date.
    /// </summary>
    [XmlElement("publicationDate")]
    public string? PublicationDate { get; set; }

    /// <summary>
    /// The type of publication (e.g., "Jugement", "Radiation").
    /// </summary>
    [XmlElement("type")]
    public string? Type { get; set; }

    /// <summary>
    /// The family of publication (e.g., "Procédures collectives").
    /// </summary>
    [XmlElement("family")]
    public string? Family { get; set; }

    /// <summary>
    /// The machine-readable family code (e.g., "radiation", "collective", "vente", "creation", "modification", "dpc").
    /// More reliable than <see cref="Family"/> for programmatic checks.
    /// </summary>
    [XmlElement("familyCode")]
    public string? FamilyCode { get; set; }

    /// <summary>
    /// The company name from the publication.
    /// </summary>
    [XmlElement("companyName")]
    public string? CompanyName { get; set; }

    /// <summary>
    /// Serialized JSON persons list (<c>listepersonnes</c>).
    /// </summary>
    [XmlElement("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Serialized JSON radiation data (<c>radiationaurcs</c>).
    /// Present when the company is struck off the trade register.
    /// </summary>
    [XmlElement("radiation")]
    public string? Radiation { get; set; }

    /// <summary>
    /// Serialized JSON court judgment data (<c>jugement</c>).
    /// Present for collective proceedings (insolvency, liquidation, etc.).
    /// </summary>
    [XmlElement("judgment")]
    public string? Judgment { get; set; }

    /// <summary>
    /// Direct URL to the full record on www.bodacc.fr.
    /// </summary>
    [XmlElement("detailUrl")]
    public string? DetailUrl { get; set; }
}
