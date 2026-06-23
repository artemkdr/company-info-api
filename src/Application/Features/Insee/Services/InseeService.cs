using System.Text.Json;
using CompanyInfo.Api.Shared.Attributes;
using Microsoft.Extensions.Caching.Memory;

namespace CompanyInfo.Api.Application.Features.Insee;

/// <summary>
/// Service that looks up French establishments via the INSEE SIRENE API.
/// Uses API key authentication via the X-INSEE-Api-Key-Integration header.
/// Results are cached using IMemoryCache with configurable TTL.
/// </summary>
[RegisterService(ServiceLifetime.Scoped, serviceType: typeof(IInseeService))]
public class InseeService : IInseeService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;
    private readonly ILogger<InseeService> _logger;

    private const string CacheKeyPrefix = "insee:";

    /// <summary>
    /// Initializes a new instance of the <see cref="InseeService"/> class.
    /// </summary>
    public InseeService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        IConfiguration configuration,
        ILogger<InseeService> logger
    )
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<InseeEstablishmentResponse> GetEstablishmentAsync(string siret)
    {
        var cacheKey = $"{CacheKeyPrefix}{siret}";

        if (_cache.TryGetValue(cacheKey, out InseeEstablishmentResponse? cached) && cached != null)
        {
            _logger.LogDebug("INSEE cache hit for SIRET {Siret}", siret);
            return cached;
        }

        var response = await FetchEstablishmentAsync(siret);

        var expirationMinutes = _configuration.GetValue<int>("Cache:InseeExpirationMinutes", 1440);
        _cache.Set(cacheKey, response, TimeSpan.FromMinutes(expirationMinutes));

        return response;
    }

    private async Task<InseeEstablishmentResponse> FetchEstablishmentAsync(string siret)
    {
        try
        {
            var baseUrl =
                _configuration["ExternalApis:Insee:BaseUrl"]
                ?? "https://api.insee.fr/api-sirene/3.11";
            var apiKey = _configuration["ExternalApis:Insee:ApiKey"];

            if (string.IsNullOrEmpty(apiKey))
            {
                return new InseeEstablishmentResponse
                {
                    Siret = siret,
                    Found = false,
                    ErrorMessage = "INSEE API key is not configured.",
                };
            }

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("X-INSEE-Api-Key-Integration", apiKey);

            var url = $"{baseUrl}/siret/{siret}";
            var response = await client.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                return new InseeEstablishmentResponse
                {
                    Siret = siret,
                    Found = false,
                    ErrorMessage = $"INSEE API returned status {(int)response.StatusCode}.",
                };
            }

            var json = await response.Content.ReadAsStringAsync();
            return ParseInseeResponse(json, siret);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "INSEE lookup failed for SIRET {Siret}", siret);
            return new InseeEstablishmentResponse
            {
                Siret = siret,
                Found = false,
                ErrorMessage = $"Lookup failed: {ex.Message}",
            };
        }
    }

    private static InseeEstablishmentResponse ParseInseeResponse(string json, string siret)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("etablissement", out var establishment))
        {
            return new InseeEstablishmentResponse
            {
                Siret = siret,
                Found = false,
                ErrorMessage = "Unexpected response format from INSEE API.",
            };
        }

        var legalUnit = establishment.TryGetProperty("uniteLegale", out var lu) ? lu : default;

        var estAddress = establishment.TryGetProperty("adresseEtablissement", out var addr)
            ? addr
            : default;

        var activityCode = GetString(legalUnit, "activitePrincipaleUniteLegale") ?? "";
        var adminState = GetString(legalUnit, "etatAdministratifUniteLegale");
        var isActive = adminState == "A";
        var isAutomotive = activityCode.StartsWith("45");

        // Get trade name from periods
        string? tradeName = null;
        if (establishment.TryGetProperty("periodesEtablissement", out var periods))
        {
            var periodsArray = periods.EnumerateArray().ToList();
            if (periodsArray.Count > 0)
            {
                tradeName = GetString(periodsArray[0], "enseigne1Etablissement");
            }
        }

        var siren = siret.Length >= 9 ? siret[..9] : siret;

        return new InseeEstablishmentResponse
        {
            Siret = siret,
            Siren = siren,
            CompanyName = GetString(legalUnit, "denominationUniteLegale"),
            TradeName = tradeName,
            ActivityCode = activityCode,
            LastName = GetString(legalUnit, "nomUniteLegale"),
            FirstName = GetString(legalUnit, "prenom1UniteLegale"),
            PostalCode = GetString(estAddress, "codePostalEtablissement"),
            City = GetString(estAddress, "libelleCommuneEtablissement"),
            IsActive = isActive,
            IsAutomotive = isAutomotive,
            Found = true,
        };
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Undefined)
        {
            return null;
        }

        return
            element.TryGetProperty(propertyName, out var prop)
            && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;
    }
}
