namespace CompanyInfo.Api.Application.Features.ChIde;

/// <summary>
/// Service interface for looking up and validating Swiss UID numbers
/// via the CH IDE (UID-WSE) SOAP API.
/// </summary>
public interface IChIdeService
{
    /// <summary>
    /// Looks up a company by its Swiss UID number.
    /// </summary>
    /// <param name="uid">The UID identifier (e.g., "CHE123456789" or "123456789").</param>
    /// <returns>The company information.</returns>
    Task<ChIdeGetByUidResponse> GetByUidAsync(string uid);

    /// <summary>
    /// Validates whether a Swiss UID number is registered and active.
    /// </summary>
    /// <param name="uid">The UID identifier to validate.</param>
    /// <returns>The validation result.</returns>
    Task<ChIdeValidateUidResponse> ValidateUidAsync(string uid);
}
