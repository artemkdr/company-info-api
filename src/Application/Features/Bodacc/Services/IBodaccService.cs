namespace CompanyInfo.Api.Application.Features.Bodacc;

/// <summary>
/// Service interface for searching BODACC (Bulletin Officiel des Annonces Civiles et Commerciales)
/// records to determine company bankruptcy/dissolution status.
/// </summary>
public interface IBodaccService
{
    /// <summary>
    /// Searches BODACC records by registration number (SIREN or SIRET).
    /// </summary>
    /// <param name="registrationNumber">The SIREN (9 digits) or SIRET (14 digits) number.</param>
    /// <returns>The search results including liquidation status.</returns>
    Task<BodaccSearchResponse> SearchAsync(string registrationNumber);
}
