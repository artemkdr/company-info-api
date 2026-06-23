namespace CompanyInfo.Api.Application.Features.Vies;

/// <summary>
/// Service interface for validating EU VAT numbers via the VIES system.
/// </summary>
public interface IViesService
{
    /// <summary>
    /// Validates the format of a VAT number locally without calling any external service.
    /// This check never throws and always returns immediately.
    /// </summary>
    /// <param name="vatNumber">The VAT number to validate (e.g., "FR12345678901").</param>
    /// <returns>The format validation result.</returns>
    ViesFormatValidationResponse ValidateFormat(string vatNumber);

    /// <summary>
    /// Checks whether a VAT number is currently active in the EU VIES system.
    /// Calls the external VIES API with retry logic for transient errors.
    /// Results are cached.
    /// </summary>
    /// <param name="vatNumber">The VAT number to check (e.g., "FR12345678901").</param>
    /// <returns>The active-check result.</returns>
    Task<ViesValidationResponse> CheckActiveAsync(string vatNumber);
}
