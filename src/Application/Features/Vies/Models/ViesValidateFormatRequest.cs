using System.ComponentModel;

namespace CompanyInfo.Api.Application.Features.Vies;

/// <summary>
/// Shared request model for VIES format validation.
/// </summary>
public class ViesValidateFormatRequest
{
    /// <summary>
    /// The EU VAT number to validate locally (e.g., "FR12345678901").
    /// </summary>
    [Description("The EU VAT number to validate (e.g., 'FR12345678901').")]
    public string VatNumber { get; set; } = string.Empty;
}
