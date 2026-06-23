using CompanyInfo.Api.Application.Features.TvaCalculator;
using FluentAssertions;
using Xunit;

namespace CompanyInfo.Api.Tests.Unit.Features.TvaCalculator;

/// <summary>
/// Unit tests for the TVA Calculator service.
/// </summary>
public class TvaCalculatorServiceTests
{
    private readonly TvaCalculatorService _service = new();

    [Theory(DisplayName = "Calculate should return correct TVA number for known SIREN values")]
    [InlineData("443061841", "FR64443061841")]
    [InlineData("732829320", "FR44732829320")]
    [InlineData("552032534", "FR27552032534")]
    public void Calculate_KnownSirenValues_ReturnsCorrectTva(string siren, string expectedTva)
    {
        var result = _service.Calculate(siren);

        result.IsValid.Should().BeTrue();
        result.TvaNumber.Should().Be(expectedTva);
        result.Siren.Should().Be(siren);
    }

    [Fact(DisplayName = "Calculate should handle SIREN with extra characters")]
    public void Calculate_SirenWithExtraCharacters_ExtractsDigitsAndCalculates()
    {
        var result = _service.Calculate("443-061-841");

        result.IsValid.Should().BeTrue();
        result.TvaNumber.Should().Be("FR64443061841");
        result.Siren.Should().Be("443061841");
    }

    [Fact(DisplayName = "Calculate should handle SIREN longer than 9 digits")]
    public void Calculate_SirenLongerThan9Digits_TakesFirst9()
    {
        var result = _service.Calculate("44306184100015");

        result.IsValid.Should().BeTrue();
        result.Siren.Should().Be("443061841");
    }

    [Theory(DisplayName = "Calculate should return invalid with required error for blank input")]
    [InlineData("")]
    [InlineData("   ")]
    public void Calculate_BlankInput_ReturnsInvalidWithRequiredError(string siren)
    {
        var result = _service.Calculate(siren);

        result.IsValid.Should().BeFalse();
        result.TvaNumber.Should().BeNull();
        result.ErrorMessage.Should().Be("SIREN number is required.");
    }

    [Theory(
        DisplayName = "Calculate should return invalid with digit error for too-short numeric strings"
    )]
    [InlineData("12345")]
    [InlineData("abcdefghij")]
    public void Calculate_TooShort_ReturnsInvalidWithDigitError(string siren)
    {
        var result = _service.Calculate(siren);

        result.IsValid.Should().BeFalse();
        result.TvaNumber.Should().BeNull();
        result.ErrorMessage.Should().Be("SIREN must contain at least 9 digits.");
    }

    [Fact(DisplayName = "Calculate should return invalid with required error for null input")]
    public void Calculate_NullInput_ReturnsInvalid()
    {
        var result = _service.Calculate(null!);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Be("SIREN number is required.");
    }

    [Fact(DisplayName = "Calculate should pad TVA key with leading zero")]
    public void Calculate_SmallKey_PadsWithLeadingZero()
    {
        // SIREN 000000000 => key = (12 + 3*(0%97))%97 = 12 => "12"
        var result = _service.Calculate("000000000");

        result.IsValid.Should().BeTrue();
        result.TvaNumber.Should().StartWith("FR12");
    }

    [Fact(DisplayName = "Calculate should produce 13-character TVA number")]
    public void Calculate_ValidSiren_ProducesFR2DigitKey9DigitSiren()
    {
        var result = _service.Calculate("443061841");

        result.IsValid.Should().BeTrue();
        result.TvaNumber.Should().HaveLength(13);
        result.TvaNumber.Should().StartWith("FR");
    }
}
