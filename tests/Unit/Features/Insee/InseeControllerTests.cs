using CompanyInfo.Api.Application.Features.Insee;
using CompanyInfo.Api.Shared.Validation;
using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace CompanyInfo.Api.Tests.Unit.Features.Insee;

/// <summary>
/// Unit tests for <see cref="InseeController"/>.
/// </summary>
public class InseeControllerTests
{
    [Fact(
        DisplayName = "GetEstablishment should return 200 OK with service result for valid request"
    )]
    public async Task GetEstablishment_ValidRequest_ReturnsOk()
    {
        var inseeService = Substitute.For<IInseeService>();
        var requestValidationService = Substitute.For<IRequestValidationService>();
        var controller = new InseeController(inseeService, requestValidationService);
        var request = new InseeGetEstablishmentRequest { Siret = "443 061 841 00015" };
        var expectedResponse = new InseeEstablishmentResponse
        {
            Siret = "44306184100015",
            Found = true,
        };

        inseeService.GetEstablishmentAsync("44306184100015").Returns(expectedResponse);

        var result = await controller.GetEstablishment(request);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeSameAs(expectedResponse);
        await requestValidationService
            .Received(1)
            .ValidateAndThrowAsync(request, Arg.Any<CancellationToken>());
        await inseeService.Received(1).GetEstablishmentAsync("44306184100015");
    }

    [Fact(
        DisplayName = "GetEstablishment should throw validation exception and not call service for invalid request"
    )]
    public async Task GetEstablishment_InvalidRequest_ThrowsValidationException()
    {
        var inseeService = Substitute.For<IInseeService>();
        var requestValidationService = Substitute.For<IRequestValidationService>();
        var controller = new InseeController(inseeService, requestValidationService);
        var request = new InseeGetEstablishmentRequest { Siret = string.Empty };
        var validationException = new ValidationException("SIRET number is required.");

        requestValidationService
            .ValidateAndThrowAsync(request, Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw validationException);

        var action = async () => await controller.GetEstablishment(request);

        await action
            .Should()
            .ThrowAsync<ValidationException>()
            .WithMessage("SIRET number is required.");
        await inseeService.DidNotReceive().GetEstablishmentAsync(Arg.Any<string>());
    }
}
