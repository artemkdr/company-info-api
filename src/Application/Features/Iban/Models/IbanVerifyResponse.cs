using System.Xml.Serialization;
using CompanyInfo.Api.Shared.Contracts;

namespace CompanyInfo.Api.Application.Features.Iban;

/// <summary>
/// Response model for IBAN verification and local BIC resolution.
/// </summary>
[XmlRoot("IbanVerification")]
public class IbanVerifyResponse : IExternalValidationResponse
{
    /// <summary>
    /// The normalized IBAN value used for validation.
    /// </summary>
    [XmlElement("normalizedIban")]
    public string NormalizedIban { get; set; } = string.Empty;

    /// <summary>
    /// The two-letter country code extracted from the IBAN.
    /// </summary>
    [XmlElement("countryCode")]
    public string CountryCode { get; set; } = string.Empty;

    /// <summary>
    /// Indicates whether the country code is known in the local registry.
    /// </summary>
    [XmlElement("isKnownCountry")]
    public bool IsKnownCountry { get; set; }

    /// <summary>
    /// The expected IBAN length for the country, or 0 if the country is unknown.
    /// </summary>
    [XmlElement("expectedLength")]
    public int ExpectedLength { get; set; }

    /// <summary>
    /// The actual length of the normalized IBAN.
    /// </summary>
    [XmlElement("actualLength")]
    public int ActualLength { get; set; }

    /// <summary>
    /// Indicates whether the normalized IBAN length matches the country rule.
    /// </summary>
    [XmlElement("isLengthValid")]
    public bool IsLengthValid { get; set; }

    /// <summary>
    /// Indicates whether the normalized IBAN checksum is valid.
    /// </summary>
    [XmlElement("isChecksumValid")]
    public bool IsChecksumValid { get; set; }

    /// <summary>
    /// Indicates whether the BBAN part matches known country-specific structure rules.
    /// </summary>
    [XmlElement("isStructureValid")]
    public bool IsStructureValid { get; set; }

    /// <summary>
    /// Indicates whether the normalized IBAN is valid overall.
    /// </summary>
    [XmlElement("isValid")]
    public bool IsValid { get; set; }

    /// <summary>
    /// The resolved BIC when a local lookup succeeds.
    /// </summary>
    [XmlElement("bic")]
    public string? Bic { get; set; }

    /// <summary>
    /// The country-specific bank code extracted from the BBAN when derivable.
    /// </summary>
    [XmlElement("bankCode")]
    public string? BankCode { get; set; }

    /// <summary>
    /// The branch code extracted from the BBAN when derivable.
    /// </summary>
    [XmlElement("branchCode")]
    public string? BranchCode { get; set; }

    /// <summary>
    /// The account number or account identifier extracted from the BBAN when derivable.
    /// </summary>
    [XmlElement("accountNumber")]
    public string? AccountNumber { get; set; }

    /// <summary>
    /// The national check digits extracted from the BBAN when derivable.
    /// </summary>
    [XmlElement("nationalCheckDigits")]
    public string? NationalCheckDigits { get; set; }

    /// <summary>
    /// The local BIC lookup status.
    /// </summary>
    [XmlElement("bicLookupStatus")]
    public string BicLookupStatus { get; set; } = "not_attempted";

    /// <summary>
    /// The provenance of the resolved BIC or lookup table.
    /// </summary>
    [XmlElement("bicLookupSource")]
    public string? BicLookupSource { get; set; }

    /// <summary>
    /// The institution name read from the local lookup table when available.
    /// </summary>
    [XmlElement("bankName")]
    public string? BankName { get; set; }

    /// <summary>
    /// Error details for malformed or unsupported input, or <see langword="null"/> if validation succeeded.
    /// </summary>
    /// <remarks>
    /// HTTP status is always 200 OK. A non-<see langword="null"/> value means the IBAN was rejected
    /// (malformed, invalid checksum, unsupported country, etc.); callers should treat the result as invalid.
    /// </remarks>
    [XmlElement("errorMessage")]
    public string? ErrorMessage { get; set; }
}
