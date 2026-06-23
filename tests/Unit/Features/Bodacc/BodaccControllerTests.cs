using CompanyInfo.Api.Application.Features.Bodacc;
using CompanyInfo.Api.Shared.Validation;
using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace CompanyInfo.Api.Tests.Unit.Features.Bodacc;

/// <summary>
/// Unit tests for <see cref="BodaccController"/>.
/// </summary>
public class BodaccControllerTests
{
    [Fact(DisplayName = "Search should return 200 OK with service result for valid request")]
    public async Task Search_ValidRequest_ReturnsOk()
    {
        var bodaccService = Substitute.For<IBodaccService>();
        var requestValidationService = Substitute.For<IRequestValidationService>();
        var controller = new BodaccController(bodaccService, requestValidationService);
        var request = new BodaccSearchRequest { RegistrationNumber = "443 061 841 00015" };
        var expectedResponse = new BodaccSearchResponse { RegistrationNumber = "44306184100015" };

        bodaccService.SearchAsync("44306184100015").Returns(Task.FromResult(expectedResponse));

        var result = await controller.Search(request);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeSameAs(expectedResponse);
        await requestValidationService
            .Received(1)
            .ValidateAndThrowAsync(request, Arg.Any<CancellationToken>());
        await bodaccService.Received(1).SearchAsync("44306184100015");
    }

    [Fact(
        DisplayName = "Search should throw validation exception and not call service for invalid request"
    )]
    public async Task Search_InvalidRequest_ThrowsValidationException()
    {
        var bodaccService = Substitute.For<IBodaccService>();
        var requestValidationService = Substitute.For<IRequestValidationService>();
        var controller = new BodaccController(bodaccService, requestValidationService);
        var request = new BodaccSearchRequest { RegistrationNumber = string.Empty };
        var validationException = new ValidationException(
            "Registration number (SIREN or SIRET) is required."
        );

        requestValidationService
            .ValidateAndThrowAsync(request, Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw validationException);

        var action = async () => await controller.Search(request);

        await action
            .Should()
            .ThrowAsync<ValidationException>()
            .WithMessage("Registration number (SIREN or SIRET) is required.");
        await bodaccService.DidNotReceive().SearchAsync(Arg.Any<string>());
    }
}
