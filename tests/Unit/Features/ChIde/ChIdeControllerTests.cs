using CompanyInfo.Api.Application.Features.ChIde;
using CompanyInfo.Api.Shared.Validation;
using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace CompanyInfo.Api.Tests.Unit.Features.ChIde;

/// <summary>
/// Unit tests for <see cref="ChIdeController"/>.
/// </summary>
public class ChIdeControllerTests
{
    [Fact(DisplayName = "GetByUid should return 200 OK with service result for valid request")]
    public async Task GetByUid_ValidRequest_ReturnsOk()
    {
        var chIdeService = Substitute.For<IChIdeService>();
        var requestValidationService = Substitute.For<IRequestValidationService>();
        var controller = new ChIdeController(chIdeService, requestValidationService);
        var request = new ChIdeGetByUidRequest { Uid = "CHE-123.456.789" };
        var expectedResponse = new ChIdeGetByUidResponse
        {
            Uid = "CHE-123.456.789",
            OrganisationName = "Test Corp",
        };

        chIdeService.GetByUidAsync("CHE-123.456.789").Returns(Task.FromResult(expectedResponse));

        var result = await controller.GetByUid(request);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeSameAs(expectedResponse);
        await requestValidationService
            .Received(1)
            .ValidateAndThrowAsync(request, Arg.Any<CancellationToken>());
        await chIdeService.Received(1).GetByUidAsync("CHE-123.456.789");
    }

    [Fact(
        DisplayName = "GetByUid should throw validation exception and not call service for invalid request"
    )]
    public async Task GetByUid_InvalidRequest_ThrowsValidationException()
    {
        var chIdeService = Substitute.For<IChIdeService>();
        var requestValidationService = Substitute.For<IRequestValidationService>();
        var controller = new ChIdeController(chIdeService, requestValidationService);
        var request = new ChIdeGetByUidRequest { Uid = string.Empty };
        var validationException = new ValidationException("UID is required.");

        requestValidationService
            .ValidateAndThrowAsync(request, Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw validationException);

        var action = async () => await controller.GetByUid(request);

        await action.Should().ThrowAsync<ValidationException>().WithMessage("UID is required.");
        await chIdeService.DidNotReceive().GetByUidAsync(Arg.Any<string>());
    }

    [Fact(DisplayName = "ValidateUid should return 200 OK with service result for valid request")]
    public async Task ValidateUid_ValidRequest_ReturnsOk()
    {
        var chIdeService = Substitute.For<IChIdeService>();
        var requestValidationService = Substitute.For<IRequestValidationService>();
        var controller = new ChIdeController(chIdeService, requestValidationService);
        var request = new ChIdeValidateUidRequest { Uid = "CHE-123.456.789" };
        var expectedResponse = new ChIdeValidateUidResponse
        {
            Uid = "CHE-123.456.789",
            IsValid = true,
        };

        chIdeService.ValidateUidAsync("CHE-123.456.789").Returns(Task.FromResult(expectedResponse));

        var result = await controller.ValidateUid(request);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeSameAs(expectedResponse);
        await requestValidationService
            .Received(1)
            .ValidateAndThrowAsync(request, Arg.Any<CancellationToken>());
        await chIdeService.Received(1).ValidateUidAsync("CHE-123.456.789");
    }

    [Fact(
        DisplayName = "ValidateUid should throw validation exception and not call service for invalid request"
    )]
    public async Task ValidateUid_InvalidRequest_ThrowsValidationException()
    {
        var chIdeService = Substitute.For<IChIdeService>();
        var requestValidationService = Substitute.For<IRequestValidationService>();
        var controller = new ChIdeController(chIdeService, requestValidationService);
        var request = new ChIdeValidateUidRequest { Uid = string.Empty };
        var validationException = new ValidationException("UID is required.");

        requestValidationService
            .ValidateAndThrowAsync(request, Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw validationException);

        var action = async () => await controller.ValidateUid(request);

        await action.Should().ThrowAsync<ValidationException>().WithMessage("UID is required.");
        await chIdeService.DidNotReceive().ValidateUidAsync(Arg.Any<string>());
    }
}
