using System.ComponentModel;

namespace CompanyInfo.Api.Application.Features.Vies;

/// <summary>
/// Shared request model for VIES active check.
/// </summary>
public class ViesCheckActiveRequest
{
    /// <summary>
    /// The EU VAT number to check for active status (e.g., "FR12345678901").
    /// </summary>
    [Description("The EU VAT number to check for active status (e.g., 'FR12345678901').")]
    public string VatNumber { get; set; } = string.Empty;
}
