using CompanyInfo.Api.Application.Features.Iban;
using CompanyInfo.Api.Application.Features.Iban.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace CompanyInfo.Api.Tests.Unit.Features.Iban;

/// <summary>
/// Unit tests for <see cref="IbanRegistryParser"/>.
/// Covers IBAN structure patterns, country lengths, bank/branch positions,
/// component extraction, and BIC lookup key generation.
/// </summary>
public class IbanRegistryParserTests
{
    private readonly ILogger<IbanRegistryParser> _logger = Substitute.For<
        ILogger<IbanRegistryParser>
    >();

    private IbanRegistryParser CreateParser() => new IbanRegistryParser(_logger, (string?)null);

    /// <summary>
    /// LoadCountryIbanRegex tests: Verifies IBAN structure validation patterns
    /// </summary>
    [Fact(DisplayName = "LoadCountryIbanRegex should load regex patterns for all countries")]
    public void LoadCountryIbanRegex_ReturnsPatternDictionary()
    {
        var parser = CreateParser();

        var regexPatterns = parser.LoadCountryIbanRegex();

        regexPatterns.Should().NotBeEmpty();
        regexPatterns.Count.Should().BeGreaterThanOrEqualTo(5, "at least fallback countries");
    }

    [Theory(DisplayName = "LoadCountryIbanRegex should contain patterns for major countries")]
    [InlineData("DE")]
    [InlineData("FR")]
    [InlineData("ES")]
    [InlineData("NL")]
    public void LoadCountryIbanRegex_ContainsMajorCountries(string countryCode)
    {
        var parser = CreateParser();

        var regexPatterns = parser.LoadCountryIbanRegex();

        regexPatterns.Should().ContainKey(countryCode);
        regexPatterns[countryCode].Should().StartWith("^").And.EndWith("$");
    }

    [Theory(DisplayName = "LoadCountryIbanRegex patterns should be valid for known IBANs")]
    [InlineData("DE", "DE75512108001245126199")]
    [InlineData("FR", "FR7630006000011234567890189")]
    [InlineData("ES", "ES7921000813610123456789")]
    [InlineData("NL", "NL02ABNA0123456789")]
    public void LoadCountryIbanRegex_PatternsValidateCorrectly(string countryCode, string validIban)
    {
        var parser = CreateParser();
        var regexPatterns = parser.LoadCountryIbanRegex();

        regexPatterns.Should().ContainKey(countryCode);
        var regex = new System.Text.RegularExpressions.Regex(
            regexPatterns[countryCode],
            System.Text.RegularExpressions.RegexOptions.CultureInvariant
        );
        regex.IsMatch(validIban).Should().BeTrue();
    }

    [Theory(DisplayName = "LoadCountryIbanRegex patterns should reject invalid IBANs")]
    [InlineData("DE", "GB75512108001245126199")] // Wrong country code
    [InlineData("FR", "DE7630006000011234567890189")] // Wrong country code
    public void LoadCountryIbanRegex_PatternsRejectInvalid(string countryCode, string invalidIban)
    {
        var parser = CreateParser();
        var regexPatterns = parser.LoadCountryIbanRegex();

        regexPatterns.Should().ContainKey(countryCode);
        var regex = new System.Text.RegularExpressions.Regex(
            regexPatterns[countryCode],
            System.Text.RegularExpressions.RegexOptions.CultureInvariant
        );
        regex.IsMatch(invalidIban).Should().BeFalse();
    }

    /// <summary>
    /// LoadCountryLengths tests: Verifies IBAN length lookup
    /// </summary>
    [Fact(DisplayName = "LoadCountryLengths should load length dictionary")]
    public void LoadCountryLengths_ReturnsLengthDictionary()
    {
        var parser = CreateParser();

        var lengths = parser.LoadCountryLengths();

        lengths.Should().NotBeEmpty();
        lengths.Count.Should().BeGreaterThanOrEqualTo(5, "at least fallback countries");
    }

    [Theory(DisplayName = "LoadCountryLengths should contain correct lengths for major countries")]
    [InlineData("DE", 22)]
    [InlineData("FR", 27)]
    [InlineData("ES", 24)]
    [InlineData("NL", 18)]
    public void LoadCountryLengths_ContainsCorrectLengths(string countryCode, int expectedLength)
    {
        var parser = CreateParser();

        var lengths = parser.LoadCountryLengths();

        lengths.Should().ContainKey(countryCode);
        lengths[countryCode].Should().Be(expectedLength);
    }

    [Fact(DisplayName = "LoadCountryLengths should have all values > 4")]
    public void LoadCountryLengths_AllLengthsGreaterThanCountryPrefix()
    {
        var parser = CreateParser();

        var lengths = parser.LoadCountryLengths();

        foreach (var (country, length) in lengths)
        {
            length
                .Should()
                .BeGreaterThan(4, $"IBAN length for {country} must include country+check prefix");
        }
    }

    /// <summary>
    /// LoadBankBranchPositions tests: Verifies component position lookup
    /// </summary>
    [Fact(DisplayName = "LoadBankBranchPositions should load positions dictionary")]
    public void LoadBankBranchPositions_ReturnsPositionsDictionary()
    {
        var parser = CreateParser();

        var positions = parser.LoadBankBranchPositions();

        positions.Should().NotBeEmpty();
        positions.Count.Should().BeGreaterThanOrEqualTo(11, "at least fallback countries");
    }

    [Theory(DisplayName = "LoadBankBranchPositions should contain positions for major countries")]
    [InlineData("DE")]
    [InlineData("FR")]
    [InlineData("ES")]
    [InlineData("NL")]
    public void LoadBankBranchPositions_ContainsMajorCountries(string countryCode)
    {
        var parser = CreateParser();

        var positions = parser.LoadBankBranchPositions();

        positions.Should().ContainKey(countryCode);
        var pos = positions[countryCode];
        pos.BankOffset.Should().BeGreaterThanOrEqualTo(4);
        pos.BankLength.Should().BeGreaterThan(0);
    }

    [Theory(DisplayName = "LoadBankBranchPositions should have valid offsets")]
    [InlineData("DE")]
    [InlineData("FR")]
    public void LoadBankBranchPositions_OffsetsAreValid(string countryCode)
    {
        var parser = CreateParser();

        var positions = parser.LoadBankBranchPositions();

        var pos = positions[countryCode];
        pos.BankOffset.Should().Be(4, "bank offset should start after country+check prefix");
        pos.BankLength.Should().BeGreaterThan(0);
    }

    [Fact(DisplayName = "LoadBankBranchPositions should handle branch positions correctly")]
    public void LoadBankBranchPositions_BranchPositionsOptional()
    {
        var parser = CreateParser();

        var positions = parser.LoadBankBranchPositions();

        var dePos = positions["DE"];
        dePos.BranchOffset.Should().BeNull("DE does not require branch code");

        var esPos = positions["ES"];
        esPos.BranchOffset.Should().NotBeNull("ES has branch position");
        esPos.BranchLength.Should().NotBeNull();
    }

    /// <summary>
    /// PopulateNationalComponents tests: Verifies component extraction
    /// </summary>
    [Fact(DisplayName = "PopulateNationalComponents should extract DE bank code")]
    public void PopulateNationalComponents_ExtractsGermanBankCode()
    {
        var parser = CreateParser();
        var response = new IbanVerifyResponse
        {
            CountryCode = "DE",
            NormalizedIban = "DE75512108001245126199",
        };

        parser.PopulateNationalComponents(response);

        response.BankCode.Should().Be("51210800");
        response.BranchCode.Should().BeNullOrEmpty();
        response.AccountNumber.Should().Be("1245126199");
    }

    [Fact(DisplayName = "PopulateNationalComponents should extract FR components with branch")]
    public void PopulateNationalComponents_ExtractsFrenchComponentsWithBranch()
    {
        var parser = CreateParser();
        var response = new IbanVerifyResponse
        {
            CountryCode = "FR",
            NormalizedIban = "FR7630006000011234567890189",
        };

        parser.PopulateNationalComponents(response);

        response.BankCode.Should().Be("30006");
        response.BranchCode.Should().Be("00001", "FR has branch in BBAN");
        response.AccountNumber.Should().Be("12345678901");
        response.NationalCheckDigits.Should().Be("89");
    }

    [Fact(DisplayName = "PopulateNationalComponents should extract ES components")]
    public void PopulateNationalComponents_ExtractsSpanishComponents()
    {
        var parser = CreateParser();
        var response = new IbanVerifyResponse
        {
            CountryCode = "ES",
            NormalizedIban = "ES7921000813610123456789",
        };

        parser.PopulateNationalComponents(response);

        response.BankCode.Should().Be("2100");
        response.BranchCode.Should().Be("0813");
        response.NationalCheckDigits.Should().Be("61");
        response.AccountNumber.Should().Be("0123456789");
    }

    [Fact(DisplayName = "PopulateNationalComponents should extract NL bank code")]
    public void PopulateNationalComponents_ExtractsDutchBankCode()
    {
        var parser = CreateParser();
        var response = new IbanVerifyResponse
        {
            CountryCode = "NL",
            NormalizedIban = "NL02ABNA0123456789",
        };

        parser.PopulateNationalComponents(response);

        response.BankCode.Should().Be("ABNA");
        response.AccountNumber.Should().Be("0123456789");
    }

    [Fact(DisplayName = "PopulateNationalComponents should extract IT components")]
    public void PopulateNationalComponents_ExtractsItalianComponents()
    {
        var parser = CreateParser();
        var response = new IbanVerifyResponse
        {
            CountryCode = "IT",
            NormalizedIban = "IT60X0542811101000000123456",
        };

        parser.PopulateNationalComponents(response);

        response.BankCode.Should().Be("05428");
        response.BranchCode.Should().Be("11101");
        response.NationalCheckDigits.Should().Be("X");
        response.AccountNumber.Should().Be("000000123456");
    }

    [Fact(DisplayName = "PopulateNationalComponents should handle CH correctly")]
    public void PopulateNationalComponents_ExtractsSwissComponents()
    {
        var parser = CreateParser();
        var response = new IbanVerifyResponse
        {
            CountryCode = "CH",
            NormalizedIban = "CH5604835012345678009",
        };

        parser.PopulateNationalComponents(response);

        response.BankCode.Should().Be("04835");
        response.AccountNumber.Should().Be("012345678009");
    }

    [Fact(DisplayName = "PopulateNationalComponents should handle CZ correctly")]
    public void PopulateNationalComponents_ExtractsCzechComponents()
    {
        var parser = CreateParser();
        var response = new IbanVerifyResponse
        {
            CountryCode = "CZ",
            NormalizedIban = "CZ6508000000192000145399",
        };

        parser.PopulateNationalComponents(response);

        response.BankCode.Should().Be("0800");
        response.AccountNumber.Should().Be("2000145399");
    }

    [Fact(DisplayName = "PopulateNationalComponents should handle unsupported country")]
    public void PopulateNationalComponents_UnknownCountry_NoExtraction()
    {
        var parser = CreateParser();
        var response = new IbanVerifyResponse
        {
            CountryCode = "XX",
            NormalizedIban = "XX75512108001245126199",
        };

        parser.PopulateNationalComponents(response);

        response.BankCode.Should().BeNullOrEmpty("unsupported country has no extraction rules");
    }

    /// <summary>
    /// GetLookupKeys tests: Verifies BIC lookup key generation
    /// </summary>
    [Fact(
        DisplayName = "GetLookupKeys should generate default key for all countries with bank code"
    )]
    public void GetLookupKeys_GeneratesDefaultKey()
    {
        var parser = CreateParser();
        var response = new IbanVerifyResponse
        {
            CountryCode = "DE",
            BankCode = "51210800",
            BranchCode = null,
        };

        var keys = parser.GetLookupKeys(response).ToList();

        keys.Should().ContainSingle().Which.Should().Be("DE:51210800");
    }

    [Fact(DisplayName = "GetLookupKeys should generate branch-specific key for FR when available")]
    public void GetLookupKeys_FrenchWithBranch_GeneratesBranchKey()
    {
        var parser = CreateParser();
        var response = new IbanVerifyResponse
        {
            CountryCode = "FR",
            BankCode = "30006",
            BranchCode = "00001",
        };

        var keys = parser.GetLookupKeys(response).ToList();

        keys.Should().HaveCount(2);
        keys[0].Should().Be("FR:30006:00001");
        keys[1].Should().Be("FR:30006");
    }

    [Fact(DisplayName = "GetLookupKeys should generate default key for FR without branch")]
    public void GetLookupKeys_FrenchWithoutBranch_GeneratesDefaultKey()
    {
        var parser = CreateParser();
        var response = new IbanVerifyResponse
        {
            CountryCode = "FR",
            BankCode = "30006",
            BranchCode = null,
        };

        var keys = parser.GetLookupKeys(response).ToList();

        keys.Should().ContainSingle().Which.Should().Be("FR:30006");
    }

    [Fact(DisplayName = "GetLookupKeys should generate branch-specific key for MC when available")]
    public void GetLookupKeys_MonacoWithBranch_GeneratesBranchKey()
    {
        var parser = CreateParser();
        var response = new IbanVerifyResponse
        {
            CountryCode = "MC",
            BankCode = "20041",
            BranchCode = "01005",
        };

        var keys = parser.GetLookupKeys(response).ToList();

        keys.Should().HaveCount(2);
        keys[0].Should().Be("MC:20041:01005");
        keys[1].Should().Be("MC:20041");
    }

    [Fact(DisplayName = "GetLookupKeys should generate key for ES")]
    public void GetLookupKeys_Spanish_GeneratesKey()
    {
        var parser = CreateParser();
        var response = new IbanVerifyResponse
        {
            CountryCode = "ES",
            BankCode = "2100",
            BranchCode = "0813",
        };

        var keys = parser.GetLookupKeys(response).ToList();

        keys.Should().ContainSingle().Which.Should().Be("ES:2100");
    }

    [Fact(DisplayName = "GetLookupKeys should generate key for NL")]
    public void GetLookupKeys_Dutch_GeneratesKey()
    {
        var parser = CreateParser();
        var response = new IbanVerifyResponse { CountryCode = "NL", BankCode = "ABNA" };

        var keys = parser.GetLookupKeys(response).ToList();

        keys.Should().ContainSingle().Which.Should().Be("NL:ABNA");
    }

    [Fact(DisplayName = "GetLookupKeys should generate key for any country with bank code")]
    public void GetLookupKeys_ArbitraryCountry_GeneratesDefaultKey()
    {
        var parser = CreateParser();
        var response = new IbanVerifyResponse { CountryCode = "AT", BankCode = "12000" };

        var keys = parser.GetLookupKeys(response).ToList();

        keys.Should().ContainSingle().Which.Should().Be("AT:12000");
    }

    [Fact(DisplayName = "GetLookupKeys should return empty when bank code is missing")]
    public void GetLookupKeys_NoBankCode_ReturnsEmpty()
    {
        var parser = CreateParser();
        var response = new IbanVerifyResponse { CountryCode = "DE", BankCode = null };

        var keys = parser.GetLookupKeys(response).ToList();

        keys.Should().BeEmpty();
    }

    [Fact(DisplayName = "GetLookupKeys should return empty when bank code is whitespace")]
    public void GetLookupKeys_WhitespaceBankCode_ReturnsEmpty()
    {
        var parser = CreateParser();
        var response = new IbanVerifyResponse { CountryCode = "DE", BankCode = "   " };

        var keys = parser.GetLookupKeys(response).ToList();

        keys.Should().BeEmpty();
    }

    [Theory(DisplayName = "GetLookupKeys should support all countries with extracted bank codes")]
    [InlineData("DE")]
    [InlineData("FR")]
    [InlineData("ES")]
    [InlineData("NL")]
    [InlineData("CH")]
    [InlineData("IT")]
    [InlineData("AT")]
    [InlineData("BE")]
    [InlineData("GB")]
    [InlineData("GR")]
    public void GetLookupKeys_AllCountries_GenerateKeys(string countryCode)
    {
        var parser = CreateParser();
        var response = new IbanVerifyResponse { CountryCode = countryCode, BankCode = "TEST123" };

        var keys = parser.GetLookupKeys(response).ToList();

        keys.Should().NotBeEmpty();
        keys[0].Should().Be($"{countryCode}:TEST123");
    }
}
