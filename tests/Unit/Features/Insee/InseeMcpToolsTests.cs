using CompanyInfo.Api.Application.Features.Insee;
using CompanyInfo.Api.Shared.Validation;
using FluentAssertions;
using FluentValidation;
using ModelContextProtocol.Server;
using NSubstitute;
using Xunit;

namespace CompanyInfo.Api.Tests.Unit.Features.Insee;

/// <summary>
/// Unit tests for <see cref="InseeMcpTools"/>.
/// </summary>
public class InseeMcpToolsTests
{
    [Fact(
        DisplayName = "MCP Insee tool should call service with normalized SIRET for valid request"
    )]
    public async Task GetEstablishmentInfo_ValidRequest_CallsServiceWithNormalizedSiret()
    {
        var inseeService = Substitute.For<IInseeService>();
        var requestValidationService = Substitute.For<IRequestValidationService>();
        var tool = new InseeMcpTools(inseeService, requestValidationService);
        var request = new InseeGetEstablishmentRequest { Siret = "443.061.841-00015" };
        var expectedResponse = new InseeEstablishmentResponse
        {
            Siret = "44306184100015",
            Found = true,
        };

        inseeService.GetEstablishmentAsync("44306184100015").Returns(expectedResponse);

        var result = await tool.GetEstablishmentInfo(request);

        result.Should().BeSameAs(expectedResponse);
        await requestValidationService
            .Received(1)
            .ValidateAndThrowAsync(request, Arg.Any<CancellationToken>());
        await inseeService.Received(1).GetEstablishmentAsync("44306184100015");
    }

    [Fact(
        DisplayName = "MCP Insee tool should throw validation exception and not call service for invalid request"
    )]
    public async Task GetEstablishmentInfo_InvalidRequest_ThrowsValidationException()
    {
        var inseeService = Substitute.For<IInseeService>();
        var requestValidationService = Substitute.For<IRequestValidationService>();
        var tool = new InseeMcpTools(inseeService, requestValidationService);
        var request = new InseeGetEstablishmentRequest { Siret = string.Empty };
        var validationException = new ValidationException("SIRET number is required.");

        requestValidationService
            .ValidateAndThrowAsync(request, Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw validationException);

        var action = async () => await tool.GetEstablishmentInfo(request);

        await action
            .Should()
            .ThrowAsync<ValidationException>()
            .WithMessage("SIRET number is required.");
        await inseeService.DidNotReceive().GetEstablishmentAsync(Arg.Any<string>());
    }

    [Fact(DisplayName = "MCP Insee tool should have explicit stable snake_case tool name")]
    public void GetEstablishmentInfo_ShouldExposeExplicitToolName()
    {
        var toolType = typeof(InseeMcpTools);
        var classAttribute = toolType.GetCustomAttributes(
            typeof(McpServerToolTypeAttribute),
            false
        );
        var method = toolType.GetMethod(nameof(InseeMcpTools.GetEstablishmentInfo));

        method.Should().NotBeNull();
        classAttribute.Should().NotBeNull();
        classAttribute.Length.Should().Be(1);

        var toolAttribute = (McpServerToolAttribute?)
            method!.GetCustomAttributes(typeof(McpServerToolAttribute), false).SingleOrDefault();

        toolAttribute.Should().NotBeNull();
        toolAttribute!.Name.Should().Be("get_establishment_info");
        toolAttribute.ReadOnly.Should().BeTrue();
        toolAttribute.Idempotent.Should().BeTrue();
    }
}
