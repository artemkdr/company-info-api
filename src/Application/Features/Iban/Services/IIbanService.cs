namespace CompanyInfo.Api.Application.Features.Iban;

/// <summary>
/// Service interface for local IBAN verification and BIC resolution.
/// </summary>
public interface IIbanService
{
    /// <summary>
    /// Verifies an IBAN locally and attempts to resolve its BIC from the local lookup table.
    /// </summary>
    /// <param name="iban">The IBAN to verify.</param>
    /// <returns>The verification and lookup result.</returns>
    Task<IbanVerifyResponse> VerifyAsync(string iban);
}
