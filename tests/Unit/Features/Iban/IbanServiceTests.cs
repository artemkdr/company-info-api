using CompanyInfo.Api.Application.Features.Iban;
using CompanyInfo.Api.Application.Features.Iban.Services;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace CompanyInfo.Api.Tests.Unit.Features.Iban;

/// <summary>
/// Unit tests for <see cref="IbanService"/>.
/// </summary>
public class IbanServiceTests
{
    private readonly ILogger<IbanService> _logger = Substitute.For<ILogger<IbanService>>();
    private readonly ILogger<IbanRegistryParser> _registryParserLogger = Substitute.For<
        ILogger<IbanRegistryParser>
    >();
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());
    private readonly IConfiguration _configuration;

    public IbanServiceTests()
    {
        var configData = new Dictionary<string, string?>
        {
            { "Cache:IbanExpirationMinutes", "1440" },
        };
        _configuration = new ConfigurationBuilder().AddInMemoryCollection(configData).Build();
    }

    /// <summary>
    /// Creates an IbanService instance with a mock BIC data provider for testing.
    /// </summary>
    /// <remarks>
    /// Mock provider returns deterministic dictionaries, allowing tests to verify validation logic
    /// without depending on local CSV files. Tests use the real validation code path.
    /// </remarks>
    private IbanService CreateServiceWithMockProvider()
    {
        var csvProvider = Substitute.For<IBicDataProvider>();
        var externalProvider = Substitute.For<IExternalBicLookupService>();
        csvProvider
            .LoadBicData()
            .Returns(
                new Dictionary<string, BicLookupEntry>(StringComparer.OrdinalIgnoreCase)
                {
                    {
                        "DE:51210800",
                        new BicLookupEntry("INGDDEFFXXX", "51210800", null, null, "test")
                    },
                    { "FR:30006", new BicLookupEntry("AGRIFRPPXXX", "30006", null, null, "test") },
                    {
                        "FR:30006:00001",
                        new BicLookupEntry("AGRIFRPPXXX", "30006", "00001", null, "test")
                    },
                    { "NL:ABNA", new BicLookupEntry("ABNANL2AXXX", "ABNA", null, null, "test") },
                    { "ES:2100", new BicLookupEntry("CAIXESBBXXX", "2100", null, null, "test") },
                }
            );
        csvProvider.ProviderName.Returns("CSV (Mock)");

        externalProvider
            .TryResolveAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(ExternalBicLookupResult.NotFound("test-external")));
        externalProvider.ProviderName.Returns("test-external");

        var registryParser = new IbanRegistryParser(_registryParserLogger, _configuration);

        return new IbanService(
            _logger,
            csvProvider,
            externalProvider,
            registryParser,
            _cache,
            _configuration
        );
    }

    [Fact(DisplayName = "Verify should use external fallback when local lookup misses")]
    public async Task Verify_LocalLookupMiss_UsesExternalFallback()
    {
        var csvProvider = Substitute.For<IBicDataProvider>();
        csvProvider
            .LoadBicData()
            .Returns(new Dictionary<string, BicLookupEntry>(StringComparer.OrdinalIgnoreCase));
        csvProvider.ProviderName.Returns("CSV (Mock)");

        var externalProvider = Substitute.For<IExternalBicLookupService>();
        externalProvider
            .TryResolveAsync("DE75512108001245126199", "DE")
            .Returns(
                Task.FromResult(
                    new ExternalBicLookupResult(
                        true,
                        "TESTDEFFXXX",
                        "51210800",
                        "Fallback Bank",
                        "openiban"
                    )
                )
            );

        var registryParser = new IbanRegistryParser(_registryParserLogger, _configuration);

        var service = new IbanService(
            _logger,
            csvProvider,
            externalProvider,
            registryParser,
            _cache,
            _configuration
        );

        var result = await service.VerifyAsync("DE75512108001245126199");

        result.IsValid.Should().BeTrue();
        result.Bic.Should().Be("TESTDEFFXXX");
        result.BankName.Should().Be("Fallback Bank");
        result.BicLookupSource.Should().Be("openiban");
        result.BicLookupStatus.Should().Be("found_fallback");
    }

    [Theory(DisplayName = "Verify should accept known valid IBAN values")]
    [InlineData("DE75512108001245126199", "DE", "INGDDEFFXXX")]
    [InlineData("FR7630006000011234567890189", "FR", "AGRIFRPPXXX")]
    [InlineData("NL02ABNA0123456789", "NL", "ABNANL2AXXX")]
    public async Task Verify_ValidIban_ReturnsValidResult(
        string iban,
        string countryCode,
        string expectedBic
    )
    {
        var service = CreateServiceWithMockProvider();

        var result = await service.VerifyAsync(iban);

        result.IsValid.Should().BeTrue();
        result.CountryCode.Should().Be(countryCode);
        result.IsLengthValid.Should().BeTrue();
        result.IsStructureValid.Should().BeTrue();
        result.IsChecksumValid.Should().BeTrue();
        result.Bic.Should().Be(expectedBic);
        result.BicLookupStatus.Should().Be("found");
    }

    [Fact(DisplayName = "Verify should normalize spaces and dashes before validation")]
    public async Task Verify_IbanWithSeparators_NormalizesAndValidates()
    {
        var service = CreateServiceWithMockProvider();

        var result = await service.VerifyAsync("de75-5121 0800 1245 1261 99");

        result.NormalizedIban.Should().Be("DE75512108001245126199");
        result.IsValid.Should().BeTrue();
        result.Bic.Should().Be("INGDDEFFXXX");
    }

    [Fact(DisplayName = "Verify should reject IBAN with wrong length for known country")]
    public async Task Verify_WrongLength_ReturnsInvalid()
    {
        var service = CreateServiceWithMockProvider();

        var result = await service.VerifyAsync("DE7551210800124512619");

        result.IsValid.Should().BeFalse();
        result.IsLengthValid.Should().BeFalse();
        result.IsStructureValid.Should().BeFalse();
        result.IsChecksumValid.Should().BeFalse();
        result.BicLookupStatus.Should().Be("invalid_iban");
    }

    [Fact(DisplayName = "Verify should reject IBAN with invalid checksum")]
    public async Task Verify_InvalidChecksum_ReturnsInvalid()
    {
        var service = CreateServiceWithMockProvider();

        var result = await service.VerifyAsync("DE75512108001245126198");

        result.IsValid.Should().BeFalse();
        result.IsLengthValid.Should().BeTrue();
        result.IsStructureValid.Should().BeTrue();
        result.IsChecksumValid.Should().BeFalse();
        result.BicLookupStatus.Should().Be("invalid_iban");
    }

    [Fact(DisplayName = "Verify should return unsupported status for unknown country code")]
    public async Task Verify_UnknownCountry_ReturnsUnsupportedCountry()
    {
        var service = CreateServiceWithMockProvider();

        var result = await service.VerifyAsync("ZZ75512108001245126199");

        result.IsKnownCountry.Should().BeFalse();
        result.IsValid.Should().BeFalse();
        result.BicLookupStatus.Should().Be("unsupported_country");
    }

    [Fact(DisplayName = "Verify should return found when local BIC entry exists")]
    public async Task Verify_ValidIbanWithLookupEntry_ReturnsFound()
    {
        var service = CreateServiceWithMockProvider();

        var result = await service.VerifyAsync("ES7921000813610123456789");

        result.IsValid.Should().BeTrue();
        result.BankCode.Should().Be("2100");
        result.BicLookupStatus.Should().Be("found");
        result.Bic.Should().Be("CAIXESBBXXX");
    }

    [Fact(DisplayName = "Verify should reject unsupported characters")]
    public async Task Verify_UnsupportedCharacters_ReturnsInvalid()
    {
        var service = CreateServiceWithMockProvider();

        var result = await service.VerifyAsync("DE75!512108001245126199");

        result.IsValid.Should().BeFalse();
        result
            .ErrorMessage.Should()
            .Be("IBAN may only contain letters, digits, spaces, or dashes.");
    }

    [Fact(DisplayName = "Verify should reject IBAN with invalid country/check prefix format")]
    public async Task Verify_InvalidPrefixFormat_ReturnsInvalid()
    {
        var service = CreateServiceWithMockProvider();

        var result = await service.VerifyAsync("D175512108001245126199");

        result.IsValid.Should().BeFalse();
        result.BicLookupStatus.Should().Be("invalid_iban");
        result
            .ErrorMessage.Should()
            .Be("IBAN prefix must start with two letters followed by two digits.");
    }

    [Fact(DisplayName = "Verify should reject IBAN with BBAN structure mismatch")]
    public async Task Verify_BbanStructureMismatch_ReturnsInvalid()
    {
        var service = CreateServiceWithMockProvider();

        // NL BBAN must be 4 letters + 10 digits. Here one non-digit is injected in account part.
        var result = await service.VerifyAsync("NL02ABNA01234A6789");

        result.IsValid.Should().BeFalse();
        result.IsLengthValid.Should().BeTrue();
        result.IsStructureValid.Should().BeFalse();
        result.IsChecksumValid.Should().BeFalse();
        result.BicLookupStatus.Should().Be("invalid_iban");
        result.ErrorMessage.Should().Be("IBAN BBAN structure mismatch for country NL.");
    }

    [Fact(DisplayName = "Verify should accept valid IT IBAN structure from registry rules")]
    public async Task Verify_ValidItalianIban_PassesStructureValidation()
    {
        var service = CreateServiceWithMockProvider();

        var result = await service.VerifyAsync("IT60X0542811101000000123456");

        result.IsValid.Should().BeTrue();
        result.IsStructureValid.Should().BeTrue();
        result.BankCode.Should().Be("05428");
        result.BicLookupStatus.Should().Be("not_found");
    }

    [Fact(
        DisplayName = "Verify should reject IT IBAN with BBAN structure mismatch from registry rules"
    )]
    public async Task Verify_InvalidItalianBbanStructure_ReturnsInvalid()
    {
        var service = CreateServiceWithMockProvider();

        // IT BBAN starts with one alphabetic character; here it is replaced by a digit.
        var result = await service.VerifyAsync("IT6010542811101000000123456");

        result.IsValid.Should().BeFalse();
        result.IsLengthValid.Should().BeTrue();
        result.IsStructureValid.Should().BeFalse();
        result.IsChecksumValid.Should().BeFalse();
        result.BicLookupStatus.Should().Be("invalid_iban");
        result.ErrorMessage.Should().Be("IBAN BBAN structure mismatch for country IT.");
    }

    [Fact(DisplayName = "Verify should extract CH bank code and attempt BIC lookup")]
    public async Task Verify_ValidSwissIban_ExtractsBankCodeAndLooksUp()
    {
        var service = CreateServiceWithMockProvider();

        var result = await service.VerifyAsync("CH5604835012345678009");

        result.IsValid.Should().BeTrue();
        result.CountryCode.Should().Be("CH");
        result.BankCode.Should().Be("04835");
        result.BicLookupStatus.Should().Be("not_found");
    }
}
