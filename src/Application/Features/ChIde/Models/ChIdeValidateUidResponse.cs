using System.Xml.Serialization;
using CompanyInfo.Api.Shared.Contracts;

namespace CompanyInfo.Api.Application.Features.ChIde;

/// <summary>
/// Response model for CH IDE UID validation.
/// </summary>
[XmlRoot("ChIdeValidation")]
public class ChIdeValidateUidResponse : IExternalValidationResponse
{
    /// <summary>
    /// The UID number that was validated.
    /// </summary>
    [XmlElement("uid")]
    public string Uid { get; set; } = string.Empty;

    /// <summary>
    /// Whether the UID is valid and registered.
    /// </summary>
    [XmlElement("isValid")]
    public bool IsValid { get; set; }

    /// <summary>
    /// Error message returned by the CH IDE external service, or <see langword="null"/> if validation succeeded.
    /// </summary>
    /// <remarks>
    /// HTTP status is always 200 OK. A non-<see langword="null"/> value means the external service
    /// rejected the request or returned an error; callers should treat the result as invalid.
    /// </remarks>
    [XmlElement("errorMessage")]
    public string? ErrorMessage { get; set; }
}
