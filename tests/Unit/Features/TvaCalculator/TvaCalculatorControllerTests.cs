using CompanyInfo.Api.Application.Features.TvaCalculator;
using CompanyInfo.Api.Shared.Validation;
using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace CompanyInfo.Api.Tests.Unit.Features.TvaCalculator;

/// <summary>
/// Unit tests for <see cref="TvaCalculatorController"/>.
/// </summary>
public class TvaCalculatorControllerTests
{
    [Fact(DisplayName = "Calculate should return 200 OK with valid TVA result for valid SIREN")]
    public async Task Calculate_ValidSiren_ReturnsOk()
    {
        var tvaCalculatorService = Substitute.For<ITvaCalculatorService>();
        var requestValidationService = Substitute.For<IRequestValidationService>();
        var controller = new TvaCalculatorController(
            tvaCalculatorService,
            requestValidationService
        );
        var request = new CalculateTvaRequest { Siren = "123456789" };
        var expectedResponse = new CalculateTvaResponse
        {
            Siren = "123456789",
            TvaNumber = "FR26123456789",
            IsValid = true,
        };

        tvaCalculatorService.Calculate("123456789").Returns(expectedResponse);

        var result = await controller.Calculate(request);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeSameAs(expectedResponse);
        await requestValidationService
            .Received(1)
            .ValidateAndThrowAsync(request, Arg.Any<CancellationToken>());
        tvaCalculatorService.Received(1).Calculate("123456789");
    }

    [Fact(
        DisplayName = "Calculate should throw validation exception and not call service for empty SIREN"
    )]
    public async Task Calculate_EmptySiren_ThrowsValidationException()
    {
        var tvaCalculatorService = Substitute.For<ITvaCalculatorService>();
        var requestValidationService = Substitute.For<IRequestValidationService>();
        var controller = new TvaCalculatorController(
            tvaCalculatorService,
            requestValidationService
        );
        var request = new CalculateTvaRequest { Siren = string.Empty };
        var validationException = new ValidationException("SIREN number is required.");

        requestValidationService
            .ValidateAndThrowAsync(request, Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw validationException);

        var action = async () => await controller.Calculate(request);

        await action
            .Should()
            .ThrowAsync<ValidationException>()
            .WithMessage("SIREN number is required.");
        tvaCalculatorService.DidNotReceive().Calculate(Arg.Any<string>());
    }

    [Fact(
        DisplayName = "Calculate should throw validation exception and not call service for invalid SIREN"
    )]
    public async Task Calculate_InvalidSiren_ThrowsValidationException()
    {
        var tvaCalculatorService = Substitute.For<ITvaCalculatorService>();
        var requestValidationService = Substitute.For<IRequestValidationService>();
        var controller = new TvaCalculatorController(
            tvaCalculatorService,
            requestValidationService
        );
        var request = new CalculateTvaRequest { Siren = "123" };
        var validationException = new ValidationException(
            "SIREN number must be exactly 9 or 14 digits."
        );

        requestValidationService
            .ValidateAndThrowAsync(request, Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw validationException);

        var action = async () => await controller.Calculate(request);

        await action
            .Should()
            .ThrowAsync<ValidationException>()
            .WithMessage("SIREN number must be exactly 9 or 14 digits.");
        await requestValidationService
            .Received(1)
            .ValidateAndThrowAsync(request, Arg.Any<CancellationToken>());
        tvaCalculatorService.DidNotReceive().Calculate(Arg.Any<string>());
    }
}
