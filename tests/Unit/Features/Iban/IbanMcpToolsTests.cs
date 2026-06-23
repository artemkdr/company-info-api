using CompanyInfo.Api.Application.Features.Iban;
using CompanyInfo.Api.Shared.Validation;
using FluentAssertions;
using FluentValidation;
using ModelContextProtocol.Server;
using NSubstitute;
using Xunit;

namespace CompanyInfo.Api.Tests.Unit.Features.Iban;

/// <summary>
/// Unit tests for <see cref="IbanMcpTools"/>.
/// </summary>
public class IbanMcpToolsTests
{
    [Fact(DisplayName = "MCP VerifyIban should throw validation exception for empty IBAN")]
    public async Task VerifyIban_EmptyIban_ThrowsValidationException()
    {
        var ibanService = Substitute.For<IIbanService>();
        var requestValidationService = Substitute.For<IRequestValidationService>();
        var tool = new IbanMcpTools(ibanService, requestValidationService);
        var request = new IbanVerifyRequest { Iban = string.Empty };
        var validationException = new ValidationException("IBAN is required.");

        requestValidationService
            .ValidateAndThrowAsync(request, Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw validationException);

        var action = async () => await tool.VerifyIban(request);

        await action.Should().ThrowAsync<ValidationException>().WithMessage("IBAN is required.");
        await ibanService.DidNotReceive().VerifyAsync(Arg.Any<string>());
    }

    [Fact(DisplayName = "MCP Iban tool should expose explicit stable tool name")]
    public void VerifyIban_ShouldExposeExplicitToolName()
    {
        var method = typeof(IbanMcpTools).GetMethod(nameof(IbanMcpTools.VerifyIban));

        method.Should().NotBeNull();

        var toolAttribute = (McpServerToolAttribute?)
            method!.GetCustomAttributes(typeof(McpServerToolAttribute), false).SingleOrDefault();

        toolAttribute.Should().NotBeNull();
        toolAttribute!.Name.Should().Be("verify_iban");
        toolAttribute.ReadOnly.Should().BeTrue();
        toolAttribute.Idempotent.Should().BeTrue();
    }
}
