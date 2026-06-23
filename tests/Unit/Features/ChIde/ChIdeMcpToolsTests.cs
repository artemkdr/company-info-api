using CompanyInfo.Api.Application.Features.ChIde;
using CompanyInfo.Api.Shared.Validation;
using FluentAssertions;
using FluentValidation;
using ModelContextProtocol.Server;
using NSubstitute;
using Xunit;

namespace CompanyInfo.Api.Tests.Unit.Features.ChIde;

/// <summary>
/// Unit tests for <see cref="ChIdeMcpTools"/>.
/// </summary>
public class ChIdeMcpToolsTests
{
    [Fact(DisplayName = "MCP GetChIdeByUid should call service with UID for valid request")]
    public async Task GetChIdeByUid_ValidRequest_CallsServiceWithUid()
    {
        var chIdeService = Substitute.For<IChIdeService>();
        var requestValidationService = Substitute.For<IRequestValidationService>();
        var tool = new ChIdeMcpTools(chIdeService, requestValidationService);
        var request = new ChIdeGetByUidRequest { Uid = "CHE-123.456.789" };
        var expectedResponse = new ChIdeGetByUidResponse
        {
            Uid = "CHE-123.456.789",
            OrganisationName = "Test Corp",
        };

        chIdeService.GetByUidAsync("CHE-123.456.789").Returns(Task.FromResult(expectedResponse));

        var result = await tool.GetChIdeByUid(request);

        result.Should().BeSameAs(expectedResponse);
        await requestValidationService
            .Received(1)
            .ValidateAndThrowAsync(request, Arg.Any<CancellationToken>());
        await chIdeService.Received(1).GetByUidAsync("CHE-123.456.789");
    }

    [Fact(DisplayName = "MCP GetChIdeByUid should throw validation exception for invalid request")]
    public async Task GetChIdeByUid_InvalidRequest_ThrowsValidationException()
    {
        var chIdeService = Substitute.For<IChIdeService>();
        var requestValidationService = Substitute.For<IRequestValidationService>();
        var tool = new ChIdeMcpTools(chIdeService, requestValidationService);
        var request = new ChIdeGetByUidRequest { Uid = string.Empty };
        var validationException = new ValidationException("UID is required.");

        requestValidationService
            .ValidateAndThrowAsync(request, Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw validationException);

        var action = async () => await tool.GetChIdeByUid(request);

        await action.Should().ThrowAsync<ValidationException>().WithMessage("UID is required.");
        await chIdeService.DidNotReceive().GetByUidAsync(Arg.Any<string>());
    }

    [Fact(DisplayName = "MCP ValidateChIdeUid should call service with UID for valid request")]
    public async Task ValidateChIdeUid_ValidRequest_CallsServiceWithUid()
    {
        var chIdeService = Substitute.For<IChIdeService>();
        var requestValidationService = Substitute.For<IRequestValidationService>();
        var tool = new ChIdeMcpTools(chIdeService, requestValidationService);
        var request = new ChIdeValidateUidRequest { Uid = "CHE-123.456.789" };
        var expectedResponse = new ChIdeValidateUidResponse
        {
            Uid = "CHE-123.456.789",
            IsValid = true,
        };

        chIdeService.ValidateUidAsync("CHE-123.456.789").Returns(Task.FromResult(expectedResponse));

        var result = await tool.ValidateChIdeUid(request);

        result.Should().BeSameAs(expectedResponse);
        await requestValidationService
            .Received(1)
            .ValidateAndThrowAsync(request, Arg.Any<CancellationToken>());
        await chIdeService.Received(1).ValidateUidAsync("CHE-123.456.789");
    }

    [Fact(
        DisplayName = "MCP ValidateChIdeUid should throw validation exception for invalid request"
    )]
    public async Task ValidateChIdeUid_InvalidRequest_ThrowsValidationException()
    {
        var chIdeService = Substitute.For<IChIdeService>();
        var requestValidationService = Substitute.For<IRequestValidationService>();
        var tool = new ChIdeMcpTools(chIdeService, requestValidationService);
        var request = new ChIdeValidateUidRequest { Uid = string.Empty };
        var validationException = new ValidationException("UID is required.");

        requestValidationService
            .ValidateAndThrowAsync(request, Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw validationException);

        var action = async () => await tool.ValidateChIdeUid(request);

        await action.Should().ThrowAsync<ValidationException>().WithMessage("UID is required.");
        await chIdeService.DidNotReceive().ValidateUidAsync(Arg.Any<string>());
    }

    [Fact(DisplayName = "MCP Ch-Ide tools should expose explicit stable tool names")]
    public void McpTools_ShouldExposeExplicitToolNames()
    {
        var getMethod = typeof(ChIdeMcpTools).GetMethod(nameof(ChIdeMcpTools.GetChIdeByUid));
        var validateMethod = typeof(ChIdeMcpTools).GetMethod(
            nameof(ChIdeMcpTools.ValidateChIdeUid)
        );

        getMethod.Should().NotBeNull();
        validateMethod.Should().NotBeNull();

        var getAttr = (McpServerToolAttribute?)
            getMethod!.GetCustomAttributes(typeof(McpServerToolAttribute), false).SingleOrDefault();
        var validateAttr = (McpServerToolAttribute?)
            validateMethod!
                .GetCustomAttributes(typeof(McpServerToolAttribute), false)
                .SingleOrDefault();

        getAttr.Should().NotBeNull();
        getAttr!.Name.Should().Be("get_ch_ide_by_uid");
        getAttr.ReadOnly.Should().BeTrue();
        getAttr.Idempotent.Should().BeTrue();

        validateAttr.Should().NotBeNull();
        validateAttr!.Name.Should().Be("validate_ch_ide_uid");
        validateAttr.ReadOnly.Should().BeTrue();
        validateAttr.Idempotent.Should().BeTrue();
    }
}
