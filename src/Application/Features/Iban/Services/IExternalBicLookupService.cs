namespace CompanyInfo.Api.Application.Features.Iban.Services;

/// <summary>
/// Abstraction for external BIC lookup providers used as fallback when local data is missing.
/// </summary>
public interface IExternalBicLookupService
{
    /// <summary>
    /// Attempts to resolve BIC and bank metadata for a validated IBAN.
    /// </summary>
    /// <param name="normalizedIban">A normalized IBAN containing only uppercase letters and digits.</param>
    /// <param name="countryCode">Two-letter country code extracted from IBAN.</param>
    /// <returns>A result object describing whether the fallback lookup succeeded.</returns>
    Task<ExternalBicLookupResult> TryResolveAsync(string normalizedIban, string countryCode);

    /// <summary>
    /// Gets a display provider name for diagnostics and telemetry.
    /// </summary>
    string ProviderName { get; }
}

/// <summary>
/// Result of an external BIC fallback lookup.
/// </summary>
/// <param name="IsFound">Whether a BIC was resolved from external source.</param>
/// <param name="Bic">Resolved BIC value when found.</param>
/// <param name="BankCode">Resolved bank code when provided by source.</param>
/// <param name="BankName">Resolved institution name when provided by source.</param>
/// <param name="Source">The source/provider identifier for auditing.</param>
public sealed record ExternalBicLookupResult(
    bool IsFound,
    string? Bic,
    string? BankCode,
    string? BankName,
    string Source
)
{
    /// <summary>
    /// Shared not-found result.
    /// </summary>
    public static ExternalBicLookupResult NotFound(string source) =>
        new(false, null, null, null, source);
}
