namespace CompanyInfo.Api.Application.Features.Insee;

/// <summary>
/// Service interface for looking up French establishments via the INSEE SIRENE API.
/// </summary>
public interface IInseeService
{
    /// <summary>
    /// Gets establishment information by SIRET number.
    /// </summary>
    /// <param name="siret">The 14-digit SIRET number.</param>
    /// <returns>The establishment information.</returns>
    Task<InseeEstablishmentResponse> GetEstablishmentAsync(string siret);
}
