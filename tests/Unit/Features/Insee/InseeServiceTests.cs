using System.Net;
using System.Text;
using CompanyInfo.Api.Application.Features.Insee;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace CompanyInfo.Api.Tests.Unit.Features.Insee;

/// <summary>
/// Unit tests for the INSEE service.
/// </summary>
public class InseeServiceTests
{
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;
    private readonly ILogger<InseeService> _logger;

    private const string SampleInseeResponse = """
        {
            "etablissement": {
                "siret": "44306184100015",
                "uniteLegale": {
                    "denominationUniteLegale": "ACME SAS",
                    "activitePrincipaleUniteLegale": "45.11A",
                    "etatAdministratifUniteLegale": "A",
                    "nomUniteLegale": "Dupont",
                    "prenom1UniteLegale": "Jean"
                },
                "adresseEtablissement": {
                    "codePostalEtablissement": "75001",
                    "libelleCommuneEtablissement": "PARIS"
                },
                "periodesEtablissement": [
                    {
                        "enseigne1Etablissement": "ACME Auto"
                    }
                ]
            }
        }
        """;

    private const string SampleInactiveResponse = """
        {
            "etablissement": {
                "siret": "44306184100015",
                "uniteLegale": {
                    "denominationUniteLegale": "CLOSED SAS",
                    "activitePrincipaleUniteLegale": "62.01Z",
                    "etatAdministratifUniteLegale": "C",
                    "nomUniteLegale": "Martin",
                    "prenom1UniteLegale": "Pierre"
                },
                "adresseEtablissement": {
                    "codePostalEtablissement": "69001",
                    "libelleCommuneEtablissement": "LYON"
                },
                "periodesEtablissement": []
            }
        }
        """;

    public InseeServiceTests()
    {
        _cache = new MemoryCache(new MemoryCacheOptions());
        _logger = Substitute.For<ILogger<InseeService>>();

        var configData = new Dictionary<string, string?>
        {
            { "Cache:InseeExpirationMinutes", "5" },
            { "ExternalApis:Insee:BaseUrl", "https://api.insee.fr/api-sirene/3.11" },
            { "ExternalApis:Insee:ApiKey", "test-api-key" },
        };
        _configuration = new ConfigurationBuilder().AddInMemoryCollection(configData).Build();
    }

    [Fact(DisplayName = "GetEstablishmentAsync should parse active automotive company correctly")]
    public async Task GetEstablishmentAsync_ActiveAutomotiveCompany_ParsedCorrectly()
    {
        var handler = new FakeHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SampleInseeResponse, Encoding.UTF8, "application/json"),
            }
        );
        var httpClient = new HttpClient(handler);
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        var service = new InseeService(httpClientFactory, _cache, _configuration, _logger);
        var result = await service.GetEstablishmentAsync("44306184100015");

        result.Found.Should().BeTrue();
        result.CompanyName.Should().Be("ACME SAS");
        result.IsActive.Should().BeTrue();
        result.IsAutomotive.Should().BeTrue();
        result.TradeName.Should().Be("ACME Auto");
        result.PostalCode.Should().Be("75001");
        result.City.Should().Be("PARIS");
        result.Siren.Should().Be("443061841");
    }

    [Fact(DisplayName = "GetEstablishmentAsync should detect inactive non-automotive company")]
    public async Task GetEstablishmentAsync_InactiveNonAutomotive_DetectedCorrectly()
    {
        var handler = new FakeHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    SampleInactiveResponse,
                    Encoding.UTF8,
                    "application/json"
                ),
            }
        );
        var httpClient = new HttpClient(handler);
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        var service = new InseeService(httpClientFactory, _cache, _configuration, _logger);
        var result = await service.GetEstablishmentAsync("44306184100015");

        result.Found.Should().BeTrue();
        result.IsActive.Should().BeFalse();
        result.IsAutomotive.Should().BeFalse();
    }

    [Fact(DisplayName = "GetEstablishmentAsync should return cached result on cache hit")]
    public async Task GetEstablishmentAsync_CacheHit_ReturnsCachedResult()
    {
        var cachedResponse = new InseeEstablishmentResponse
        {
            Siret = "44306184100015",
            CompanyName = "Cached Company",
            Found = true,
        };

        _cache.Set("insee:44306184100015", cachedResponse, TimeSpan.FromMinutes(5));

        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        var service = new InseeService(httpClientFactory, _cache, _configuration, _logger);
        var result = await service.GetEstablishmentAsync("44306184100015");

        result.CompanyName.Should().Be("Cached Company");
    }

    [Fact(DisplayName = "GetEstablishmentAsync should return error when API key is not configured")]
    public async Task GetEstablishmentAsync_NoApiKey_ReturnsError()
    {
        var noKeyConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    { "Cache:InseeExpirationMinutes", "5" },
                    { "ExternalApis:Insee:BaseUrl", "https://api.insee.fr/api-sirene/3.11" },
                    { "ExternalApis:Insee:ApiKey", "" },
                }
            )
            .Build();

        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        var service = new InseeService(httpClientFactory, _cache, noKeyConfig, _logger);
        var result = await service.GetEstablishmentAsync("44306184100015");

        result.Found.Should().BeFalse();
        result.ErrorMessage.Should().Contain("API key");
    }

    [Fact(DisplayName = "GetEstablishmentAsync should handle HTTP error responses")]
    public async Task GetEstablishmentAsync_HttpError_ReturnsErrorResponse()
    {
        var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.NotFound));
        var httpClient = new HttpClient(handler);
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        var service = new InseeService(httpClientFactory, _cache, _configuration, _logger);
        var result = await service.GetEstablishmentAsync("99999999999999");

        result.Found.Should().BeFalse();
        result.ErrorMessage.Should().Contain("404");
    }

    private class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public FakeHttpMessageHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            return Task.FromResult(_response);
        }
    }
}
