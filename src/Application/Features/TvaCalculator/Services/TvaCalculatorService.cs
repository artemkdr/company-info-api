using CompanyInfo.Api.Shared.Attributes;

namespace CompanyInfo.Api.Application.Features.TvaCalculator;

/// <summary>
/// Calculates the French TVA (VAT) intra-community number from a SIREN number.
/// Formula: FR + ((12 + 3 * (siren % 97)) % 97).PadLeft(2, '0') + siren
/// </summary>
[RegisterService(ServiceLifetime.Scoped, serviceType: typeof(ITvaCalculatorService))]
public class TvaCalculatorService : ITvaCalculatorService
{
    /// <inheritdoc />
    public CalculateTvaResponse Calculate(string siren)
    {
        if (string.IsNullOrWhiteSpace(siren))
        {
            return new CalculateTvaResponse
            {
                Siren = siren ?? string.Empty,
                IsValid = false,
                ErrorMessage = "SIREN number is required.",
            };
        }

        // Take only the first 9 digits
        var cleaned = new string(siren.Where(char.IsDigit).ToArray());
        if (cleaned.Length < 9)
        {
            return new CalculateTvaResponse
            {
                Siren = siren,
                IsValid = false,
                ErrorMessage = "SIREN must contain at least 9 digits.",
            };
        }

        cleaned = cleaned[..9];

        if (!int.TryParse(cleaned, out var sirenInt))
        {
            return new CalculateTvaResponse
            {
                Siren = siren,
                IsValid = false,
                ErrorMessage = "SIREN must be a numeric value.",
            };
        }

        var key = ((12 + 3 * (sirenInt % 97)) % 97).ToString().PadLeft(2, '0');
        var tvaNumber = $"FR{key}{cleaned}";

        return new CalculateTvaResponse
        {
            Siren = cleaned,
            TvaNumber = tvaNumber,
            IsValid = true,
        };
    }
}
