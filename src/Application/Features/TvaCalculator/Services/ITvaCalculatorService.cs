namespace CompanyInfo.Api.Application.Features.TvaCalculator;

/// <summary>
/// Service interface for calculating French TVA (VAT) numbers from SIREN numbers.
/// </summary>
public interface ITvaCalculatorService
{
    /// <summary>
    /// Calculates the French TVA number from a SIREN number.
    /// </summary>
    /// <param name="siren">The 9-digit SIREN number.</param>
    /// <returns>The TVA calculation result.</returns>
    CalculateTvaResponse Calculate(string siren);
}
