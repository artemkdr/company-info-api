using System.Net;
using System.Text;
using CompanyInfo.Api.Application.Features.Bodacc;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace CompanyInfo.Api.Tests.Unit.Features.Bodacc;

/// <summary>
/// Unit tests for the Bodacc service.
/// </summary>
public class BodaccServiceTests
{
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BodaccService> _logger;

    private const string SampleLiquidationResponse = """
        {
            "total_count": 2,
            "results": [
                {
                    "dateparution": "2024-01-15",
                    "typeavis_lib": "Jugement",
                    "familleavis_lib": "Procédures collectives",
                    "commercant": "ACME SAS",
                    "listepersonnes": "Ouverture d'une procédure de liquidation judiciaire"
                },
                {
                    "dateparution": "2023-06-01",
                    "typeavis_lib": "Modification",
                    "familleavis_lib": "Ventes et cessions",
                    "commercant": "ACME SAS",
                    "listepersonnes": "Changement de gérant"
                }
            ]
        }
        """;

    private const string SampleCleanResponse = """
        {
            "total_count": 1,
            "results": [
                {
                    "dateparution": "2023-03-10",
                    "typeavis_lib": "Immatriculation",
                    "familleavis_lib": "Création d'entreprise",
                    "commercant": "CLEAN COMPANY",
                    "listepersonnes": "Création d'entreprise"
                }
            ]
        }
        """;

    private const string SampleEmptyResponse = """
        {
            "total_count": 0,
            "results": []
        }
        """;

    public BodaccServiceTests()
    {
        _cache = new MemoryCache(new MemoryCacheOptions());
        _logger = Substitute.For<ILogger<BodaccService>>();

        var configData = new Dictionary<string, string?>
        {
            { "Cache:BodaccExpirationMinutes", "5" },
            {
                "ExternalApis:Bodacc:BaseUrl",
                "https://bodacc-datadila.opendatasoft.com/api/explore/v2.1"
            },
        };
        _configuration = new ConfigurationBuilder().AddInMemoryCollection(configData).Build();
    }

    [Fact(DisplayName = "SearchAsync should detect liquidation from response")]
    public async Task SearchAsync_LiquidationRecords_DetectsLiquidation()
    {
        var handler = new FakeHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    SampleLiquidationResponse,
                    Encoding.UTF8,
                    "application/json"
                ),
            }
        );
        var httpClient = new HttpClient(handler);
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        var service = new BodaccService(httpClientFactory, _cache, _configuration, _logger);
        var result = await service.SearchAsync("443061841");

        result.IsInLiquidation.Should().BeTrue();
        result.TotalRecords.Should().Be(2);
        result.Records.Should().HaveCount(2);
    }

    [Fact(DisplayName = "SearchAsync should not flag clean company as in liquidation")]
    public async Task SearchAsync_CleanRecords_NotInLiquidation()
    {
        var handler = new FakeHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SampleCleanResponse, Encoding.UTF8, "application/json"),
            }
        );
        var httpClient = new HttpClient(handler);
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        var service = new BodaccService(httpClientFactory, _cache, _configuration, _logger);
        var result = await service.SearchAsync("443061841");

        result.IsInLiquidation.Should().BeFalse();
        result.TotalRecords.Should().Be(1);
    }

    [Fact(DisplayName = "SearchAsync should handle empty results")]
    public async Task SearchAsync_EmptyResults_ReturnsNoRecords()
    {
        var handler = new FakeHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SampleEmptyResponse, Encoding.UTF8, "application/json"),
            }
        );
        var httpClient = new HttpClient(handler);
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        var service = new BodaccService(httpClientFactory, _cache, _configuration, _logger);
        var result = await service.SearchAsync("999999999");

        result.IsInLiquidation.Should().BeFalse();
        result.TotalRecords.Should().Be(0);
        result.Records.Should().BeEmpty();
    }

    [Fact(DisplayName = "SearchAsync should return cached result on cache hit")]
    public async Task SearchAsync_CacheHit_ReturnsCachedResult()
    {
        var cachedResponse = new BodaccSearchResponse
        {
            RegistrationNumber = "443061841",
            IsInLiquidation = true,
            TotalRecords = 1,
        };

        _cache.Set("bodacc:443061841", cachedResponse, TimeSpan.FromMinutes(5));

        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        var service = new BodaccService(httpClientFactory, _cache, _configuration, _logger);
        var result = await service.SearchAsync("443061841");

        result.IsInLiquidation.Should().BeTrue();
        result.RegistrationNumber.Should().Be("443061841");
    }

    [Fact(DisplayName = "SearchAsync should return error message on HTTP error")]
    public async Task SearchAsync_HttpError_ReturnsErrorMessage()
    {
        var handler = new FakeHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.InternalServerError)
        );
        var httpClient = new HttpClient(handler);
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        var service = new BodaccService(httpClientFactory, _cache, _configuration, _logger);

        var result = await service.SearchAsync("443061841");

        result.ErrorMessage.Should().NotBeNullOrEmpty();
        result.RegistrationNumber.Should().Be("443061841");
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
