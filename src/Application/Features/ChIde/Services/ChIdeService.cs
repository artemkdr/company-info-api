using System.Text;
using System.Xml.Linq;
using CompanyInfo.Api.Shared.Attributes;
using Microsoft.Extensions.Caching.Memory;

namespace CompanyInfo.Api.Application.Features.ChIde;

/// <summary>
/// Service that looks up and validates Swiss UID numbers via the CH IDE (UID-WSE) SOAP API.
/// Results are cached using IMemoryCache with configurable TTL.
/// </summary>
[RegisterService(ServiceLifetime.Scoped, serviceType: typeof(IChIdeService))]
public class ChIdeService : IChIdeService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChIdeService> _logger;

    private const string CacheKeyPrefix = "chide:";

    /// <summary>
    /// Initializes a new instance of the <see cref="ChIdeService"/> class.
    /// </summary>
    public ChIdeService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        IConfiguration configuration,
        ILogger<ChIdeService> logger
    )
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ChIdeGetByUidResponse> GetByUidAsync(string uid)
    {
        var cleanedId = ExtractNumericId(uid);
        var cacheKey = $"{CacheKeyPrefix}get:{cleanedId}";

        if (_cache.TryGetValue(cacheKey, out ChIdeGetByUidResponse? cached) && cached != null)
        {
            _logger.LogDebug("CH IDE cache hit for UID {Uid}", uid);
            return cached;
        }

        var response = await FetchByUidAsync(cleanedId, uid);

        var expirationMinutes = _configuration.GetValue<int>("Cache:ChIdeExpirationMinutes", 1440);
        _cache.Set(cacheKey, response, TimeSpan.FromMinutes(expirationMinutes));

        return response;
    }

    /// <inheritdoc />
    public async Task<ChIdeValidateUidResponse> ValidateUidAsync(string uid)
    {
        var cacheKey = $"{CacheKeyPrefix}validate:{uid.ToUpperInvariant()}";

        if (_cache.TryGetValue(cacheKey, out ChIdeValidateUidResponse? cached) && cached != null)
        {
            _logger.LogDebug("CH IDE validate cache hit for UID {Uid}", uid);
            return cached;
        }

        var response = await FetchValidateUidAsync(uid);

        var expirationMinutes = _configuration.GetValue<int>("Cache:ChIdeExpirationMinutes", 1440);
        _cache.Set(cacheKey, response, TimeSpan.FromMinutes(expirationMinutes));

        return response;
    }

    private async Task<ChIdeGetByUidResponse> FetchByUidAsync(string numericId, string originalUid)
    {
        try
        {
            var baseUrl =
                _configuration["ExternalApis:ChIde:BaseUrl"]
                ?? "https://www.uid-wse.admin.ch/V5.0/PublicServices.svc";

            var soapEnvelope =
                $@"<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:uid=""http://www.uid.admin.ch/xmlns/uid-wse"" xmlns:ns=""http://www.ech.ch/xmlns/eCH-0097/5"">
  <soapenv:Header/>
  <soapenv:Body>
    <uid:GetByUID>
      <uid:uid>
        <ns:uidOrganisationIdCategorie>CHE</ns:uidOrganisationIdCategorie>
        <ns:uidOrganisationId>{numericId}</ns:uidOrganisationId>
      </uid:uid>
    </uid:GetByUID>
  </soapenv:Body>
</soapenv:Envelope>";

            var responseXml = await PostSoapAsync(
                baseUrl,
                "http://www.uid.admin.ch/xmlns/uid-wse/IPublicServices/GetByUID",
                soapEnvelope
            );

            return ParseGetByUidResponse(responseXml, originalUid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CH IDE GetByUID failed for {Uid}", originalUid);
            return new ChIdeGetByUidResponse
            {
                Uid = originalUid,
                Found = false,
                ErrorMessage = $"Lookup failed: {ex.Message}",
            };
        }
    }

    private async Task<ChIdeValidateUidResponse> FetchValidateUidAsync(string uid)
    {
        try
        {
            var baseUrl =
                _configuration["ExternalApis:ChIde:BaseUrl"]
                ?? "https://www.uid-wse.admin.ch/V5.0/PublicServices.svc";

            var soapEnvelope =
                $@"<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:uid=""http://www.uid.admin.ch/xmlns/uid-wse"">
  <soapenv:Header/>
  <soapenv:Body>
    <uid:ValidateUID>
      <uid:uid>{uid}</uid:uid>
    </uid:ValidateUID>
  </soapenv:Body>
</soapenv:Envelope>";

            var responseXml = await PostSoapAsync(
                baseUrl,
                "http://www.uid.admin.ch/xmlns/uid-wse/IPublicServices/ValidateUID",
                soapEnvelope
            );

            var isValid = responseXml.Contains(
                "<ValidateUIDResult>true</ValidateUIDResult>",
                StringComparison.OrdinalIgnoreCase
            );

            return new ChIdeValidateUidResponse { Uid = uid, IsValid = isValid };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CH IDE ValidateUID failed for {Uid}", uid);
            return new ChIdeValidateUidResponse
            {
                Uid = uid,
                IsValid = false,
                ErrorMessage = $"Validation failed: {ex.Message}",
            };
        }
    }

    private async Task<string> PostSoapAsync(string url, string soapAction, string soapEnvelope)
    {
        var client = _httpClientFactory.CreateClient();
        var content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");
        content.Headers.Add("SOAPAction", soapAction);

        var response = await client.PostAsync(url, content);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }

    private static ChIdeGetByUidResponse ParseGetByUidResponse(
        string responseXml,
        string originalUid
    )
    {
        var result = new ChIdeGetByUidResponse { Uid = originalUid };

        try
        {
            var doc = XDocument.Parse(responseXml);

            // Define namespaces used in the response
            XNamespace uidNs = "http://www.uid.admin.ch/xmlns/uid-wse";
            XNamespace ech97Ns = "http://www.ech.ch/xmlns/eCH-0097/5";
            XNamespace ech98Ns = "http://www.ech.ch/xmlns/eCH-0098/5";
            XNamespace ech108Ns = "http://www.ech.ch/xmlns/eCH-0108/5";

            var uidEntity =
                doc.Descendants(uidNs + "uidEntitySearchResultItem").FirstOrDefault()
                ?? doc.Descendants(uidNs + "GetByUIDResult").FirstOrDefault();

            if (uidEntity == null)
            {
                result.Found = false;
                result.ErrorMessage = "No results found for the given UID.";
                return result;
            }

            result.Found = true;

            // Organisation name (eCH-0097/5)
            result.OrganisationName =
                uidEntity.Descendants(ech97Ns + "organisationName").FirstOrDefault()?.Value
                ?? uidEntity.Descendants("organisationName").FirstOrDefault()?.Value;

            // Address fields (eCH-0098/5)
            var address =
                uidEntity.Descendants(ech98Ns + "street").FirstOrDefault()?.Value
                ?? uidEntity.Descendants("street").FirstOrDefault()?.Value;
            var houseNumber =
                uidEntity.Descendants(ech98Ns + "houseNumber").FirstOrDefault()?.Value
                ?? uidEntity.Descendants("houseNumber").FirstOrDefault()?.Value;
            result.Street = string.IsNullOrEmpty(houseNumber)
                ? address
                : $"{address} {houseNumber}";

            result.ZipCode =
                uidEntity.Descendants(ech98Ns + "swissZipCode").FirstOrDefault()?.Value
                ?? uidEntity.Descendants("swissZipCode").FirstOrDefault()?.Value;

            result.City =
                uidEntity.Descendants(ech98Ns + "town").FirstOrDefault()?.Value
                ?? uidEntity.Descendants("town").FirstOrDefault()?.Value;

            result.Canton =
                uidEntity.Descendants(ech98Ns + "cantonAbbreviation").FirstOrDefault()?.Value
                ?? uidEntity.Descendants("cantonAbbreviation").FirstOrDefault()?.Value;

            // Legal form (eCH-0097/5, field is "legalForm" not "legalFormId")
            result.LegalForm =
                uidEntity.Descendants(ech97Ns + "legalForm").FirstOrDefault()?.Value
                ?? uidEntity.Descendants("legalForm").FirstOrDefault()?.Value;

            // Status (eCH-0108/5)
            // uidregStatusEnterpriseDetail codes:
            //   1=provisional, 2=reactivation in progress, 3=definitive (active),
            //   4=mutation in progress (active), 5=deleted, 6=permanently deleted, 7=cancelled
            result.Status =
                uidEntity
                    .Descendants(ech108Ns + "uidregStatusEnterpriseDetail")
                    .FirstOrDefault()
                    ?.Value
                ?? uidEntity.Descendants("uidregStatusEnterpriseDetail").FirstOrDefault()?.Value;

            // Active = definitive (3) or mutation in progress (4)
            result.IsActive = result.Status == "3" || result.Status == "4";

            // uidregPublicStatus: 1=public, 0=blocked
            var publicStatus =
                uidEntity.Descendants(ech108Ns + "uidregPublicStatus").FirstOrDefault()?.Value
                ?? uidEntity.Descendants("uidregPublicStatus").FirstOrDefault()?.Value;

            result.IsPublic = publicStatus == "1";
        }
        catch (Exception)
        {
            result.Found = false;
            result.ErrorMessage = "Failed to parse the UID response.";
        }

        return result;
    }

    private static string ExtractNumericId(string uid)
    {
        return new string(uid.Where(char.IsDigit).ToArray());
    }
}
