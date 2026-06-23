using System.ComponentModel;

namespace CompanyInfo.Api.Application.Features.TvaCalculator;

/// <summary>
/// Shared request model for TVA calculation.
/// </summary>
public class CalculateTvaRequest
{
    /// <summary>
    /// The 9-digit French SIREN number to calculate TVA for.
    /// </summary>
    [Description("The 9-digit French SIREN number (e.g., '123456789').")]
    public string Siren { get; set; } = string.Empty;
}
