using System.Net;
using CompanyInfo.Api.Application.Features.ChIde;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace CompanyInfo.Api.Tests.Unit.Features.ChIde;

/// <summary>
/// Unit tests for the CH IDE service.
/// </summary>
public class ChIdeServiceTests
{
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChIdeService> _logger;

    public ChIdeServiceTests()
    {
        _cache = new MemoryCache(new MemoryCacheOptions());
        _logger = Substitute.For<ILogger<ChIdeService>>();

        var configData = new Dictionary<string, string?>
        {
            { "Cache:ChIdeExpirationMinutes", "5" },
            {
                "ExternalApis:ChIde:BaseUrl",
                "https://www.uid-wse.admin.ch/V5.0/PublicServices.svc"
            },
        };
        _configuration = new ConfigurationBuilder().AddInMemoryCollection(configData).Build();
    }

    [Fact(DisplayName = "GetByUidAsync should return cached result on cache hit")]
    public async Task GetByUidAsync_CacheHit_ReturnsCachedResult()
    {
        var cachedResponse = new ChIdeGetByUidResponse
        {
            Uid = "CHE-123.456.789",
            OrganisationName = "Test AG",
            Found = true,
            IsActive = true,
        };

        _cache.Set("chide:get:123456789", cachedResponse, TimeSpan.FromMinutes(5));

        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        var service = new ChIdeService(httpClientFactory, _cache, _configuration, _logger);

        var result = await service.GetByUidAsync("CHE-123.456.789");

        result.Found.Should().BeTrue();
        result.OrganisationName.Should().Be("Test AG");
    }

    [Fact(DisplayName = "ValidateUidAsync should return cached result on cache hit")]
    public async Task ValidateUidAsync_CacheHit_ReturnsCachedResult()
    {
        var cachedResponse = new ChIdeValidateUidResponse { Uid = "CHE123456789", IsValid = true };

        _cache.Set("chide:validate:CHE123456789", cachedResponse, TimeSpan.FromMinutes(5));

        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        var service = new ChIdeService(httpClientFactory, _cache, _configuration, _logger);

        var result = await service.ValidateUidAsync("CHE123456789");

        result.IsValid.Should().BeTrue();
        result.Uid.Should().Be("CHE123456789");
    }

    [Fact(DisplayName = "GetByUidAsync should extract numeric ID from UID with separators")]
    public async Task GetByUidAsync_UidWithSeparators_ExtractsNumericId()
    {
        var cachedResponse = new ChIdeGetByUidResponse { Uid = "CHE-123.456.789", Found = true };

        // The cache key uses the numeric-only ID
        _cache.Set("chide:get:123456789", cachedResponse, TimeSpan.FromMinutes(5));

        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        var service = new ChIdeService(httpClientFactory, _cache, _configuration, _logger);

        var result = await service.GetByUidAsync("CHE-123.456.789");

        result.Found.Should().BeTrue();
    }

    [Fact(DisplayName = "GetByUidAsync should handle HTTP errors gracefully")]
    public async Task GetByUidAsync_HttpError_ReturnsErrorResponse()
    {
        var handler = new FakeHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.InternalServerError)
        );
        var httpClient = new HttpClient(handler);
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        var service = new ChIdeService(httpClientFactory, _cache, _configuration, _logger);

        var result = await service.GetByUidAsync("CHE123456789");

        result.Found.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact(DisplayName = "ValidateUidAsync should handle HTTP errors gracefully")]
    public async Task ValidateUidAsync_HttpError_ReturnsErrorResponse()
    {
        var handler = new FakeHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.InternalServerError)
        );
        var httpClient = new HttpClient(handler);
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        var service = new ChIdeService(httpClientFactory, _cache, _configuration, _logger);

        var result = await service.ValidateUidAsync("CHE123456789");

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// Fake HTTP message handler for testing.
    /// </summary>
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
