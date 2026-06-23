using System.Text.Json;
using CompanyInfo.Api.Shared.Attributes;
using Microsoft.Extensions.Caching.Memory;

namespace CompanyInfo.Api.Application.Features.Bodacc;

/// <summary>
/// Service that searches BODACC records via the OpenDataSoft Explore v2.1 API
/// to determine company bankruptcy/dissolution status.
/// Results are cached using IMemoryCache with configurable TTL.
/// </summary>
[RegisterService(ServiceLifetime.Scoped, serviceType: typeof(IBodaccService))]
public class BodaccService : IBodaccService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BodaccService> _logger;

    private const string CacheKeyPrefix = "bodacc:";
    private const string DatasetId = "annonces-commerciales";

    /// <summary>
    /// Keywords that indicate a company is in liquidation or undergoing insolvency proceedings.
    /// </summary>
    private static readonly string[] LiquidationKeywords =
    {
        "liquidation",
        "liquidateur",
        "redressement judiciaire",
        "sauvegarde",
        "plan de cession",
        "jugement d'ouverture",
        "clôture pour insuffisance",
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="BodaccService"/> class.
    /// </summary>
    public BodaccService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        IConfiguration configuration,
        ILogger<BodaccService> logger
    )
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<BodaccSearchResponse> SearchAsync(string registrationNumber)
    {
        var cacheKey = $"{CacheKeyPrefix}{registrationNumber}";

        if (_cache.TryGetValue(cacheKey, out BodaccSearchResponse? cached) && cached != null)
        {
            _logger.LogDebug("Bodacc cache hit for {RegistrationNumber}", registrationNumber);
            return cached;
        }

        var response = await FetchBodaccRecordsAsync(registrationNumber);

        var expirationMinutes = _configuration.GetValue<int>("Cache:BodaccExpirationMinutes", 720);
        _cache.Set(cacheKey, response, TimeSpan.FromMinutes(expirationMinutes));

        return response;
    }

    private async Task<BodaccSearchResponse> FetchBodaccRecordsAsync(string registrationNumber)
    {
        try
        {
            var baseUrl =
                _configuration["ExternalApis:Bodacc:BaseUrl"]
                ?? "https://www.bodacc.fr/api/explore/v2.1";

            var client = _httpClientFactory.CreateClient();

            // Search BODACC dataset by registration number (SIREN ONLY!!!)
            // we have to cut the SIRET to SIREN because BODACC only indexes by SIREN
            if (registrationNumber.Length > 9)
            {
                registrationNumber = registrationNumber.Substring(0, 9);
            }
            var url =
                $"{baseUrl}/catalog/datasets/{DatasetId}/records"
                + $"?where=registre%3A%22{registrationNumber}%22"
                + "&limit=100"
                + "&order_by=dateparution%20desc";

            var response = await client.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                return new BodaccSearchResponse
                {
                    RegistrationNumber = registrationNumber,
                    ErrorMessage = $"Bodacc API returned status {(int)response.StatusCode}.",
                };
            }

            var json = await response.Content.ReadAsStringAsync();
            return ParseBodaccResponse(json, registrationNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Bodacc search failed for {RegistrationNumber}",
                registrationNumber
            );
            return new BodaccSearchResponse
            {
                RegistrationNumber = registrationNumber,
                ErrorMessage = $"Search failed: {ex.Message}",
            };
        }
    }

    private static BodaccSearchResponse ParseBodaccResponse(string json, string registrationNumber)
    {
        var result = new BodaccSearchResponse { RegistrationNumber = registrationNumber };

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("results", out var results))
        {
            result.TotalRecords = 0;
            return result;
        }

        var records = results.EnumerateArray().ToList();
        result.TotalRecords =
            root.TryGetProperty("total_count", out var totalCount)
            && totalCount.ValueKind == JsonValueKind.Number
                ? totalCount.GetInt32()
                : records.Count;

        foreach (var record in records)
        {
            var bodaccRecord = new BodaccRecord
            {
                PublicationDate = GetString(record, "dateparution"),
                Type = GetString(record, "typeavis_lib"),
                Family = GetString(record, "familleavis_lib"),
                FamilyCode = GetString(record, "familleavis"),
                CompanyName = GetString(record, "commercant"),
                Description = GetString(record, "listepersonnes"),
                Radiation = GetString(record, "radiationaurcs"),
                Judgment = GetString(record, "jugement"),
                DetailUrl = GetString(record, "url_complete"),
            };

            result.Records.Add(bodaccRecord);
        }

        // A radiation record (familleavis = "radiation") means the company is
        // officially struck off the trade register.
        result.IsRadiated = result.Records.Any(r =>
            string.Equals(r.FamilyCode, "radiation", StringComparison.OrdinalIgnoreCase)
            || r.Radiation != null
        );

        // Collective proceedings: prefer the reliable familleavis code first,
        // then fall back to keyword matching on type/family labels and judgment text.
        result.IsInLiquidation = result.Records.Any(r =>
            string.Equals(r.FamilyCode, "collective", StringComparison.OrdinalIgnoreCase)
            || LiquidationKeywords.Any(keyword =>
                (r.Type?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false)
                || (r.Description?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false)
                || (r.Family?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false)
                || (r.Judgment?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false)
            )
        );

        return result;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return
            element.TryGetProperty(propertyName, out var prop)
            && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;
    }
}
