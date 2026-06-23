using System.Text.Json;
using CompanyInfo.Api.Shared.Attributes;

namespace CompanyInfo.Api.Application.Features.Iban.Services.ExternalProviders;

/// <summary>
/// OpenIBAN-based fallback resolver for BIC lookups.
/// </summary>
/// <remarks>
/// <strong>Registered as Scoped (via AddHttpClient in IbanServiceRegistrationExtensions):</strong>
/// This service uses <see cref="HttpClient"/>, which must be registered as Scoped when using
/// AddHttpClient() with typed clients. The typed client pattern automatically handles proper
/// lifetime management and connection pooling via the underlying HttpMessageHandler.
///
/// Note: Cannot use [RegisterService] attribute because typed HttpClient registration requires
/// special configuration in the extension method.
/// </remarks>
public sealed class OpenIbanBicLookupService : IExternalBicLookupService
{
    private static readonly HashSet<string> SupportedCountryCodes =
    [
        "AT",
        "BE",
        "CH",
        "DE",
        "LI",
        "LU",
        "NL",
    ];

    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenIbanBicLookupService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenIbanBicLookupService"/> class.
    /// </summary>
    /// <param name="httpClient">Configured HTTP client instance.</param>
    /// <param name="logger">Logger instance.</param>
    public OpenIbanBicLookupService(HttpClient httpClient, ILogger<OpenIbanBicLookupService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public string ProviderName => "openiban";

    /// <inheritdoc />
    public async Task<ExternalBicLookupResult> TryResolveAsync(
        string normalizedIban,
        string countryCode
    )
    {
        if (string.IsNullOrWhiteSpace(normalizedIban) || normalizedIban.Length < 4)
        {
            return ExternalBicLookupResult.NotFound(ProviderName);
        }

        var normalizedCountryCode = (countryCode ?? string.Empty).Trim().ToUpperInvariant();
        if (!SupportedCountryCodes.Contains(normalizedCountryCode))
        {
            return ExternalBicLookupResult.NotFound(ProviderName);
        }

        try
        {
            var escapedIban = Uri.EscapeDataString(normalizedIban);
            var requestUri = $"validate/{escapedIban}?getBIC=true&validateBankCode=true";
            using var response = await _httpClient.GetAsync(requestUri);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "OpenIBAN returned non-success status code {StatusCode} for country {CountryCode}.",
                    (int)response.StatusCode,
                    normalizedCountryCode
                );
                return ExternalBicLookupResult.NotFound(ProviderName);
            }

            var payload = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(payload);

            var root = document.RootElement;
            var isValid =
                root.TryGetProperty("valid", out var validElement)
                && validElement.ValueKind == JsonValueKind.True;
            if (!isValid || !root.TryGetProperty("bankData", out var bankData))
            {
                return ExternalBicLookupResult.NotFound(ProviderName);
            }

            var bic = TryGetStringProperty(bankData, "bic")?.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(bic))
            {
                return ExternalBicLookupResult.NotFound(ProviderName);
            }

            var bankCode = TryGetStringProperty(bankData, "bankCode")?.Trim().ToUpperInvariant();
            var bankName = TryGetStringProperty(bankData, "name")?.Trim();

            return new ExternalBicLookupResult(true, bic, bankCode, bankName, ProviderName);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "OpenIBAN lookup failed for country {CountryCode}.",
                normalizedCountryCode
            );
            return ExternalBicLookupResult.NotFound(ProviderName);
        }
    }

    private static string? TryGetStringProperty(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var valueElement))
        {
            return null;
        }

        return valueElement.ValueKind == JsonValueKind.String ? valueElement.GetString() : null;
    }
}
