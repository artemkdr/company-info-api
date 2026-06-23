using CompanyInfo.Api.Application.Features.Bodacc;
using CompanyInfo.Api.Shared.Validation;
using FluentAssertions;
using FluentValidation;
using ModelContextProtocol.Server;
using NSubstitute;
using Xunit;

namespace CompanyInfo.Api.Tests.Unit.Features.Bodacc;

/// <summary>
/// Unit tests for <see cref="BodaccMcpTools"/>.
/// </summary>
public class BodaccMcpToolsTests
{
    [Fact(
        DisplayName = "MCP Bodacc tool should call service with normalized registration number for valid request"
    )]
    public async Task SearchBodaccRecords_ValidRequest_CallsServiceWithNormalizedRegistrationNumber()
    {
        var bodaccService = Substitute.For<IBodaccService>();
        var requestValidationService = Substitute.For<IRequestValidationService>();
        var tool = new BodaccMcpTools(bodaccService, requestValidationService);
        var request = new BodaccSearchRequest { RegistrationNumber = "443 061 841 00015" };
        var expectedResponse = new BodaccSearchResponse { RegistrationNumber = "44306184100015" };

        bodaccService.SearchAsync("44306184100015").Returns(Task.FromResult(expectedResponse));

        var result = await tool.SearchBodaccRecords(request);

        result.Should().BeSameAs(expectedResponse);
        await requestValidationService
            .Received(1)
            .ValidateAndThrowAsync(request, Arg.Any<CancellationToken>());
        await bodaccService.Received(1).SearchAsync("44306184100015");
    }

    [Fact(
        DisplayName = "MCP Bodacc tool should throw validation exception and not call service for invalid request"
    )]
    public async Task SearchBodaccRecords_InvalidRequest_ThrowsValidationException()
    {
        var bodaccService = Substitute.For<IBodaccService>();
        var requestValidationService = Substitute.For<IRequestValidationService>();
        var tool = new BodaccMcpTools(bodaccService, requestValidationService);
        var request = new BodaccSearchRequest { RegistrationNumber = string.Empty };
        var validationException = new ValidationException(
            "Registration number (SIREN or SIRET) is required."
        );

        requestValidationService
            .ValidateAndThrowAsync(request, Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw validationException);

        var action = async () => await tool.SearchBodaccRecords(request);

        await action
            .Should()
            .ThrowAsync<ValidationException>()
            .WithMessage("Registration number (SIREN or SIRET) is required.");
        await bodaccService.DidNotReceive().SearchAsync(Arg.Any<string>());
    }

    [Fact(DisplayName = "MCP Bodacc tool should expose explicit stable tool name")]
    public void SearchBodaccRecords_ShouldExposeExplicitToolName()
    {
        var method = typeof(BodaccMcpTools).GetMethod(nameof(BodaccMcpTools.SearchBodaccRecords));

        method.Should().NotBeNull();

        var toolAttribute = (McpServerToolAttribute?)
            method!.GetCustomAttributes(typeof(McpServerToolAttribute), false).SingleOrDefault();

        toolAttribute.Should().NotBeNull();
        toolAttribute!.Name.Should().Be("search_bodacc_records");
        toolAttribute.ReadOnly.Should().BeTrue();
        toolAttribute.Idempotent.Should().BeTrue();
    }
}
