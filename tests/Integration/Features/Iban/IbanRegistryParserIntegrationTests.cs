using CompanyInfo.Api.Application.Features.Iban;
using CompanyInfo.Api.Application.Features.Iban.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace CompanyInfo.Api.Tests.Integration.Features.Iban;

/// <summary>
/// Integration tests for <see cref="IbanRegistryParser"/> using a mock registry text file.
/// The parser is pointed to an isolated temp file path to avoid cross-test interference.
/// </summary>
public sealed class IbanRegistryParserIntegrationTests : IDisposable
{
    private readonly ILogger<IbanRegistryParser> _logger = Substitute.For<
        ILogger<IbanRegistryParser>
    >();
    private readonly string _tempDirectory;
    private readonly string _registryFilePath;

    public IbanRegistryParserIntegrationTests()
    {
        _tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "company-info-api-tests",
            Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(_tempDirectory);
        _registryFilePath = Path.Combine(_tempDirectory, "iban-registry-rules.txt");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, true);
            }
        }
        catch
        {
            // Best-effort cleanup for temp test artifacts.
        }
    }

    private IbanRegistryParser CreateParser() => new IbanRegistryParser(_logger, _registryFilePath);

    private static string CreateMockRegistryFileContent() =>
        @"IBAN prefix country code (ISO 3166)	DE	FR	ES	NL	CH	IT	GB	AT
IBAN structure	DE2!n8!n10!n	FR2!n10!a11!n2!n	ES2!n4!n4!n1!n1!n10!n	NL2!n4!a10!n	CH2!n5!n12!n	IT2!n1!a5!n5!n12!n	GB2!n4!a6!n8!n	AT2!n5!n11!n
BBAN length	18	23	20	14	17	23	18	16
Bank identifier position within the BBAN	1-8	1-5	1-4	1-4	1-5	2-6	5-8	1-5
Branch identifier position within the BBAN	N/A	6-10	5-8	N/A	N/A	7-11	1-4	N/A";

    [Fact(DisplayName = "Parser should read IBAN patterns from mock registry file")]
    public void LoadCountryIbanRegex_ReadsFromMockFile()
    {
        File.WriteAllText(_registryFilePath, CreateMockRegistryFileContent());

        var parser = CreateParser();
        var regexPatterns = parser.LoadCountryIbanRegex();

        regexPatterns.Should().ContainKeys("DE", "GB", "AT");

        var regex = new System.Text.RegularExpressions.Regex(
            regexPatterns["DE"],
            System.Text.RegularExpressions.RegexOptions.CultureInvariant
        );
        regex.IsMatch("DE75512108001245126199").Should().BeTrue();
    }

    [Fact(DisplayName = "Parser should extract lengths from mock registry file")]
    public void LoadCountryLengths_ReadsFromMockFile()
    {
        File.WriteAllText(_registryFilePath, CreateMockRegistryFileContent());

        var parser = CreateParser();
        var lengths = parser.LoadCountryLengths();

        lengths["DE"].Should().Be(22);
        lengths["GB"].Should().Be(22);
        lengths["AT"].Should().Be(20);
    }

    [Fact(DisplayName = "Parser should extract bank branch positions from mock registry file")]
    public void LoadBankBranchPositions_ReadsFromMockFile()
    {
        File.WriteAllText(_registryFilePath, CreateMockRegistryFileContent());

        var parser = CreateParser();
        var positions = parser.LoadBankBranchPositions();

        var gbPos = positions["GB"];
        gbPos.BankOffset.Should().Be(8);
        gbPos.BankLength.Should().Be(4);
        gbPos.BranchOffset.Should().Be(4);
        gbPos.BranchLength.Should().Be(4);
    }

    [Fact(DisplayName = "Parser should extract national components using mock registry positions")]
    public void PopulateNationalComponents_UsesMockRegistryPositions()
    {
        File.WriteAllText(_registryFilePath, CreateMockRegistryFileContent());

        var parser = CreateParser();
        var response = new IbanVerifyResponse
        {
            CountryCode = "DE",
            NormalizedIban = "DE75512108001245126199",
        };

        parser.PopulateNationalComponents(response);

        response.BankCode.Should().Be("51210800");
        response.AccountNumber.Should().Be("1245126199");
    }

    [Fact(DisplayName = "Parser should use fallback when configured registry file is missing")]
    public void Parser_UsesFallback_WhenConfiguredFileMissing()
    {
        if (File.Exists(_registryFilePath))
        {
            File.Delete(_registryFilePath);
        }

        var parser = CreateParser();
        var lengths = parser.LoadCountryLengths();
        var positions = parser.LoadBankBranchPositions();

        lengths.Should().ContainKey("DE");
        lengths["DE"].Should().Be(22);
        positions.Should().ContainKey("DE");
        positions["DE"].BankOffset.Should().Be(4);
    }
}
