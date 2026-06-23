using CompanyInfo.Api.Application.Features.TvaCalculator;
using CompanyInfo.Api.Shared.Validation;
using FluentAssertions;
using FluentValidation;
using ModelContextProtocol.Server;
using NSubstitute;
using Xunit;

namespace CompanyInfo.Api.Tests.Unit.Features.TvaCalculator;

/// <summary>
/// Unit tests for <see cref="TvaCalculatorMcpTools"/>.
/// </summary>
public class TvaCalculatorMcpToolsTests
{
    [Fact(DisplayName = "MCP CalculateFrenchTva should calculate TVA for valid SIREN")]
    public async Task CalculateFrenchTva_ValidSiren_CalculatesCorrectly()
    {
        var tvaCalculatorService = Substitute.For<ITvaCalculatorService>();
        var requestValidationService = Substitute.For<IRequestValidationService>();
        var tool = new TvaCalculatorMcpTools(tvaCalculatorService, requestValidationService);
        var request = new CalculateTvaRequest { Siren = "123456789" };
        var expectedResponse = new CalculateTvaResponse
        {
            Siren = "123456789",
            TvaNumber = "FR26123456789",
            IsValid = true,
        };

        tvaCalculatorService.Calculate("123456789").Returns(expectedResponse);

        var result = await tool.CalculateFrenchTva(request);

        result.Should().BeSameAs(expectedResponse);
        await requestValidationService
            .Received(1)
            .ValidateAndThrowAsync(request, Arg.Any<CancellationToken>());
        tvaCalculatorService.Received(1).Calculate("123456789");
    }

    [Fact(DisplayName = "MCP CalculateFrenchTva should throw validation exception for empty SIREN")]
    public async Task CalculateFrenchTva_EmptySiren_ThrowsValidationException()
    {
        var tvaCalculatorService = Substitute.For<ITvaCalculatorService>();
        var requestValidationService = Substitute.For<IRequestValidationService>();
        var tool = new TvaCalculatorMcpTools(tvaCalculatorService, requestValidationService);
        var request = new CalculateTvaRequest { Siren = string.Empty };
        var validationException = new ValidationException("SIREN number is required.");

        requestValidationService
            .ValidateAndThrowAsync(request, Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw validationException);

        var action = async () => await tool.CalculateFrenchTva(request);

        await action
            .Should()
            .ThrowAsync<ValidationException>()
            .WithMessage("SIREN number is required.");
        tvaCalculatorService.DidNotReceive().Calculate(Arg.Any<string>());
    }

    [Fact(DisplayName = "MCP TvaCalculator tool should expose explicit stable tool name")]
    public void McpTools_ShouldExposeExplicitToolName()
    {
        var calculateMethod = typeof(TvaCalculatorMcpTools).GetMethod(
            nameof(TvaCalculatorMcpTools.CalculateFrenchTva)
        );

        calculateMethod.Should().NotBeNull();

        var calculateAttribute = (McpServerToolAttribute?)
            calculateMethod!
                .GetCustomAttributes(typeof(McpServerToolAttribute), false)
                .SingleOrDefault();

        calculateAttribute.Should().NotBeNull();
        calculateAttribute!.Name.Should().Be("calculate_french_tva");
        calculateAttribute.ReadOnly.Should().BeTrue();
        calculateAttribute.Idempotent.Should().BeTrue();
    }
}
