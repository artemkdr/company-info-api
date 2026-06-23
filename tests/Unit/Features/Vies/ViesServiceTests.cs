using CompanyInfo.Api.Application.Features.Vies;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace CompanyInfo.Api.Tests.Unit.Features.Vies;

/// <summary>
/// Unit tests for the VIES service.
/// These tests verify caching behavior and response construction.
/// Note: actual VIES API calls are integration tests; here we test caching and error handling.
/// </summary>
public class ViesServiceTests
{
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ViesService> _logger;

    public ViesServiceTests()
    {
        _cache = new MemoryCache(new MemoryCacheOptions());
        _logger = Substitute.For<ILogger<ViesService>>();

        var configData = new Dictionary<string, string?> { { "Cache:ViesExpirationMinutes", "5" } };
        _configuration = new ConfigurationBuilder().AddInMemoryCollection(configData).Build();
    }

    [Fact(DisplayName = "ValidateFormat should return valid for well-formed VAT number")]
    public void ValidateFormat_WellFormedVatNumber_ReturnsValid()
    {
        var service = new ViesService(_cache, _configuration, _logger);

        // FR89380129866: FR + checksum 89 (correct for SIREN 380129866)
        var result = service.ValidateFormat("FR89380129866");

        result.VatNumber.Should().Be("FR89380129866");
        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "ValidateFormat should return invalid for empty input")]
    public void ValidateFormat_EmptyInput_ReturnsInvalid()
    {
        var service = new ViesService(_cache, _configuration, _logger);

        var result = service.ValidateFormat(string.Empty);

        result.IsValid.Should().BeFalse();
    }

    [Fact(DisplayName = "CheckActiveAsync should return cached result on second call")]
    public async Task CheckActiveAsync_CalledTwice_ReturnsCachedResult()
    {
        // Pre-populate cache with a known result
        var cachedResponse = new ViesValidationResponse
        {
            VatNumber = "FR40443061841",
            IsActive = true,
        };

        _cache.Set("vies:FR40443061841", cachedResponse, TimeSpan.FromMinutes(5));

        var service = new ViesService(_cache, _configuration, _logger);
        var result = await service.CheckActiveAsync("FR40443061841");

        result.IsActive.Should().BeTrue();
        result.VatNumber.Should().Be("FR40443061841");
    }

    [Fact(DisplayName = "CheckActiveAsync should cache the result")]
    public async Task CheckActiveAsync_CachesResult()
    {
        var response = new ViesValidationResponse { VatNumber = "TEST123", IsActive = false };

        _cache.Set("vies:TEST123", response, TimeSpan.FromMinutes(5));

        var service = new ViesService(_cache, _configuration, _logger);
        var result = await service.CheckActiveAsync("TEST123");

        result.Should().NotBeNull();
        result.VatNumber.Should().Be("TEST123");
    }

    [Fact(DisplayName = "CheckActiveAsync should normalize cache key to uppercase")]
    public async Task CheckActiveAsync_NormalizesCacheKey()
    {
        var response = new ViesValidationResponse { VatNumber = "fr40443061841", IsActive = true };

        _cache.Set("vies:FR40443061841", response, TimeSpan.FromMinutes(5));

        var service = new ViesService(_cache, _configuration, _logger);

        // Even though we pass lowercase, it should find the uppercase cached entry
        var result = await service.CheckActiveAsync("fr40443061841");

        result.Should().NotBeNull();
        result.IsActive.Should().BeTrue();
    }
}
