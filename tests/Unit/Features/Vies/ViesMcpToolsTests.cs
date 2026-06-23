using CompanyInfo.Api.Application.Features.Vies;
using CompanyInfo.Api.Shared.Validation;
using FluentAssertions;
using FluentValidation;
using ModelContextProtocol.Server;
using NSubstitute;
using Xunit;

namespace CompanyInfo.Api.Tests.Unit.Features.Vies;

/// <summary>
/// Unit tests for <see cref="ViesMcpTools"/>.
/// </summary>
public class ViesMcpToolsTests
{
    [Fact(
        DisplayName = "MCP ValidateVatFormat should call service with VAT number for valid request"
    )]
    public async Task ValidateVatFormat_ValidRequest_CallsServiceWithVatNumber()
    {
        var viesService = Substitute.For<IViesService>();
        var requestValidationService = Substitute.For<IRequestValidationService>();
        var tool = new ViesMcpTools(viesService, requestValidationService);
        var request = new ViesValidateFormatRequest { VatNumber = "FR12345678901" };
        var expectedResponse = new ViesFormatValidationResponse
        {
            VatNumber = "FR12345678901",
            IsValid = true,
        };

        viesService.ValidateFormat("FR12345678901").Returns(expectedResponse);

        var result = await tool.ValidateVatFormat(request);

        result.Should().BeSameAs(expectedResponse);
        await requestValidationService
            .Received(1)
            .ValidateAndThrowAsync(request, Arg.Any<CancellationToken>());
        viesService.Received(1).ValidateFormat("FR12345678901");
    }

    [Fact(
        DisplayName = "MCP ValidateVatFormat should throw validation exception for invalid request"
    )]
    public async Task ValidateVatFormat_InvalidRequest_ThrowsValidationException()
    {
        var viesService = Substitute.For<IViesService>();
        var requestValidationService = Substitute.For<IRequestValidationService>();
        var tool = new ViesMcpTools(viesService, requestValidationService);
        var request = new ViesValidateFormatRequest { VatNumber = string.Empty };
        var validationException = new ValidationException("VAT number is required.");

        requestValidationService
            .ValidateAndThrowAsync(request, Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw validationException);

        var action = async () => await tool.ValidateVatFormat(request);

        await action
            .Should()
            .ThrowAsync<ValidationException>()
            .WithMessage("VAT number is required.");
        viesService.DidNotReceive().ValidateFormat(Arg.Any<string>());
    }

    [Fact(DisplayName = "MCP CheckVatActive should call service with VAT number for valid request")]
    public async Task CheckVatActive_ValidRequest_CallsServiceWithVatNumber()
    {
        var viesService = Substitute.For<IViesService>();
        var requestValidationService = Substitute.For<IRequestValidationService>();
        var tool = new ViesMcpTools(viesService, requestValidationService);
        var request = new ViesCheckActiveRequest { VatNumber = "FR12345678901" };
        var expectedResponse = new ViesValidationResponse
        {
            VatNumber = "FR12345678901",
            IsActive = true,
        };

        viesService.CheckActiveAsync("FR12345678901").Returns(Task.FromResult(expectedResponse));

        var result = await tool.CheckVatActive(request);

        result.Should().BeSameAs(expectedResponse);
        await requestValidationService
            .Received(1)
            .ValidateAndThrowAsync(request, Arg.Any<CancellationToken>());
        await viesService.Received(1).CheckActiveAsync("FR12345678901");
    }

    [Fact(DisplayName = "MCP CheckVatActive should throw validation exception for invalid request")]
    public async Task CheckVatActive_InvalidRequest_ThrowsValidationException()
    {
        var viesService = Substitute.For<IViesService>();
        var requestValidationService = Substitute.For<IRequestValidationService>();
        var tool = new ViesMcpTools(viesService, requestValidationService);
        var request = new ViesCheckActiveRequest { VatNumber = string.Empty };
        var validationException = new ValidationException("VAT number is required.");

        requestValidationService
            .ValidateAndThrowAsync(request, Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw validationException);

        var action = async () => await tool.CheckVatActive(request);

        await action
            .Should()
            .ThrowAsync<ValidationException>()
            .WithMessage("VAT number is required.");
        await viesService.DidNotReceive().CheckActiveAsync(Arg.Any<string>());
    }

    [Fact(DisplayName = "MCP Vies tools should expose explicit stable tool names")]
    public void McpTools_ShouldExposeExplicitToolNames()
    {
        var validateMethod = typeof(ViesMcpTools).GetMethod(nameof(ViesMcpTools.ValidateVatFormat));
        var checkActiveMethod = typeof(ViesMcpTools).GetMethod(nameof(ViesMcpTools.CheckVatActive));

        validateMethod.Should().NotBeNull();
        checkActiveMethod.Should().NotBeNull();

        var validateAttribute = (McpServerToolAttribute?)
            validateMethod!
                .GetCustomAttributes(typeof(McpServerToolAttribute), false)
                .SingleOrDefault();
        var checkActiveAttribute = (McpServerToolAttribute?)
            checkActiveMethod!
                .GetCustomAttributes(typeof(McpServerToolAttribute), false)
                .SingleOrDefault();

        validateAttribute.Should().NotBeNull();
        validateAttribute!.Name.Should().Be("validate_vat_format");
        validateAttribute.ReadOnly.Should().BeTrue();
        validateAttribute.Idempotent.Should().BeTrue();

        checkActiveAttribute.Should().NotBeNull();
        checkActiveAttribute!.Name.Should().Be("check_vat_active");
        checkActiveAttribute.ReadOnly.Should().BeTrue();
        checkActiveAttribute.Idempotent.Should().BeTrue();
    }
}
