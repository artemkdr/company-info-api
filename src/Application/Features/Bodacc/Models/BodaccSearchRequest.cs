using System.ComponentModel;

namespace CompanyInfo.Api.Application.Features.Bodacc;

/// <summary>
/// Shared request model for BODACC searches by registration number.
/// </summary>
public class BodaccSearchRequest
{
    /// <summary>
    /// The French registration number, expected to normalize to a 9-digit SIREN or 14-digit SIRET.
    /// </summary>
    [Description("The SIREN (9 digits) or SIRET (14 digits) number - French business identifier.")]
    public string RegistrationNumber { get; set; } = string.Empty;

    /// <summary>
    /// Normalizes the input by keeping digits only.
    /// </summary>
    /// <returns>The normalized registration number.</returns>
    public string GetNormalizedRegistrationNumber()
    {
        return new string((RegistrationNumber ?? string.Empty).Where(char.IsDigit).ToArray());
    }
}
