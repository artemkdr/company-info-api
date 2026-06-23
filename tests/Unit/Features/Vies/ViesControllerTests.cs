using CompanyInfo.Api.Application.Features.Vies;
using CompanyInfo.Api.Shared.Validation;
using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace CompanyInfo.Api.Tests.Unit.Features.Vies;

/// <summary>
/// Unit tests for <see cref="ViesController"/>.
/// </summary>
public class ViesControllerTests
{
    [Fact(DisplayName = "Validate should return 200 OK with service result for valid request")]
    public async Task Validate_ValidRequest_ReturnsOk()
    {
        var viesService = Substitute.For<IViesService>();
        var requestValidationService = Substitute.For<IRequestValidationService>();
        var controller = new ViesController(viesService, requestValidationService);
        var request = new ViesValidateFormatRequest { VatNumber = "FR12345678901" };
        var expectedResponse = new ViesFormatValidationResponse
        {
            VatNumber = "FR12345678901",
            IsValid = true,
        };

        viesService.ValidateFormat("FR12345678901").Returns(expectedResponse);

        var result = await controller.Validate(request);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeSameAs(expectedResponse);
        await requestValidationService
            .Received(1)
            .ValidateAndThrowAsync(request, Arg.Any<CancellationToken>());
        viesService.Received(1).ValidateFormat("FR12345678901");
    }

    [Fact(
        DisplayName = "Validate should throw validation exception and not call service for invalid request"
    )]
    public async Task Validate_InvalidRequest_ThrowsValidationException()
    {
        var viesService = Substitute.For<IViesService>();
        var requestValidationService = Substitute.For<IRequestValidationService>();
        var controller = new ViesController(viesService, requestValidationService);
        var request = new ViesValidateFormatRequest { VatNumber = string.Empty };
        var validationException = new ValidationException("VAT number is required.");

        requestValidationService
            .ValidateAndThrowAsync(request, Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw validationException);

        var action = async () => await controller.Validate(request);

        await action
            .Should()
            .ThrowAsync<ValidationException>()
            .WithMessage("VAT number is required.");
        viesService.DidNotReceive().ValidateFormat(Arg.Any<string>());
    }

    [Fact(DisplayName = "CheckActive should return 200 OK with service result for valid request")]
    public async Task CheckActive_ValidRequest_ReturnsOk()
    {
        var viesService = Substitute.For<IViesService>();
        var requestValidationService = Substitute.For<IRequestValidationService>();
        var controller = new ViesController(viesService, requestValidationService);
        var request = new ViesCheckActiveRequest { VatNumber = "FR12345678901" };
        var expectedResponse = new ViesValidationResponse
        {
            VatNumber = "FR12345678901",
            IsActive = true,
        };

        viesService.CheckActiveAsync("FR12345678901").Returns(Task.FromResult(expectedResponse));

        var result = await controller.CheckActive(request);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeSameAs(expectedResponse);
        await requestValidationService
            .Received(1)
            .ValidateAndThrowAsync(request, Arg.Any<CancellationToken>());
        await viesService.Received(1).CheckActiveAsync("FR12345678901");
    }

    [Fact(
        DisplayName = "CheckActive should throw validation exception and not call service for invalid request"
    )]
    public async Task CheckActive_InvalidRequest_ThrowsValidationException()
    {
        var viesService = Substitute.For<IViesService>();
        var requestValidationService = Substitute.For<IRequestValidationService>();
        var controller = new ViesController(viesService, requestValidationService);
        var request = new ViesCheckActiveRequest { VatNumber = string.Empty };
        var validationException = new ValidationException("VAT number is required.");

        requestValidationService
            .ValidateAndThrowAsync(request, Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw validationException);

        var action = async () => await controller.CheckActive(request);

        await action
            .Should()
            .ThrowAsync<ValidationException>()
            .WithMessage("VAT number is required.");
        await viesService.DidNotReceive().CheckActiveAsync(Arg.Any<string>());
    }
}
