using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;

namespace CompanyInfo.Api.Application.Features.Insee;

/// <summary>
/// Shared request model for INSEE establishment lookup by SIRET.
/// </summary>
public class InseeGetEstablishmentRequest
{
    /// <summary>
    /// The SIRET number, expected to contain exactly 14 digits after normalization.
    /// </summary>
    [FromRoute(Name = "Siret")]
    [Description("The 14-digit SIRET number - French business identifier.")]
    public string Siret { get; set; } = string.Empty;

    /// <summary>
    /// Normalizes the input by keeping digits only.
    /// </summary>
    /// <returns>The normalized SIRET value.</returns>
    public string GetNormalizedSiret()
    {
        return new string((Siret ?? string.Empty).Where(char.IsDigit).ToArray());
    }
}
