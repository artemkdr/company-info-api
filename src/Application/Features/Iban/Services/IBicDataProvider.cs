namespace CompanyInfo.Api.Application.Features.Iban.Services;

/// <summary>
/// Abstraction for loading BIC lookup data from pluggable sources.
/// </summary>
public interface IBicDataProvider
{
    /// <summary>
    /// Loads BIC lookup entries from the provider's data source.
    /// </summary>
    /// <returns>A dictionary mapping lookup keys to BIC lookup entries.</returns>
    IReadOnlyDictionary<string, BicLookupEntry> LoadBicData();

    /// <summary>
    /// Gets the display name of the provider for logging.
    /// </summary>
    string ProviderName { get; }
}

/// <summary>
/// Represents a single BIC lookup entry.
/// </summary>
public sealed record BicLookupEntry(
    string Bic,
    string? BankCode,
    string? BranchCode,
    string? BankName,
    string Source
);
