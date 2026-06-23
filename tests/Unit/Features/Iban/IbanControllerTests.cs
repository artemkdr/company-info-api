using CompanyInfo.Api.Application.Features.Iban;
using CompanyInfo.Api.Shared.Validation;
using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace CompanyInfo.Api.Tests.Unit.Features.Iban;

/// <summary>
/// Unit tests for <see cref="IbanController"/>.
/// </summary>
public class IbanControllerTests
{
    [Fact(DisplayName = "Verify should return 200 OK with service result for valid IBAN")]
    public async Task Verify_ValidIban_ReturnsOk()
    {
        var ibanService = Substitute.For<IIbanService>();
        var requestValidationService = Substitute.For<IRequestValidationService>();
        var controller = new IbanController(ibanService, requestValidationService);
        var request = new IbanVerifyRequest { Iban = "DE75 5121 0800 1245 1261 99" };
        var expectedResponse = new IbanVerifyResponse
        {
            NormalizedIban = "DE75512108001245126199",
            CountryCode = "DE",
            IsValid = true,
            Bic = "INGDDEFFXXX",
        };

        ibanService.VerifyAsync("DE75 5121 0800 1245 1261 99").Returns(expectedResponse);

        var result = await controller.Verify(request);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(expectedResponse);
        await requestValidationService
            .Received(1)
            .ValidateAndThrowAsync(request, Arg.Any<CancellationToken>());
        await ibanService.Received(1).VerifyAsync("DE75 5121 0800 1245 1261 99");
    }

    [Fact(
        DisplayName = "Verify should throw validation exception and not call service for empty IBAN"
    )]
    public async Task Verify_EmptyIban_ThrowsValidationException()
    {
        var ibanService = Substitute.For<IIbanService>();
        var requestValidationService = Substitute.For<IRequestValidationService>();
        var controller = new IbanController(ibanService, requestValidationService);
        var request = new IbanVerifyRequest { Iban = string.Empty };
        var validationException = new ValidationException("IBAN is required.");

        requestValidationService
            .ValidateAndThrowAsync(request, Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw validationException);

        var action = async () => await controller.Verify(request);

        await action.Should().ThrowAsync<ValidationException>().WithMessage("IBAN is required.");
        await ibanService.DidNotReceive().VerifyAsync(Arg.Any<string>());
    }
}
