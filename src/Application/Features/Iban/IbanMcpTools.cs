using System.ComponentModel;
using CompanyInfo.Api.Shared.Validation;
using ModelContextProtocol.Server;

namespace CompanyInfo.Api.Application.Features.Iban;

/// <summary>
/// MCP tools for local IBAN verification and BIC resolution.
/// </summary>
[McpServerToolType]
public class IbanMcpTools
{
    private readonly IIbanService _ibanService;
    private readonly IRequestValidationService _requestValidationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="IbanMcpTools"/> class.
    /// </summary>
    /// <param name="ibanService">The IBAN verification service.</param>
    /// <param name="requestValidationService">The shared request validation service.</param>
    public IbanMcpTools(
        IIbanService ibanService,
        IRequestValidationService requestValidationService
    )
    {
        _ibanService = ibanService;
        _requestValidationService = requestValidationService;
    }

    /// <summary>
    /// Verifies an IBAN locally using country length rules and MOD-97,
    /// then attempts to resolve the BIC from a local lookup table and external provider if necessary as a fallback.
    /// </summary>
    /// <param name="request">The request containing the IBAN to verify.</param>
    /// <returns>The IBAN verification result and any resolved BIC.</returns>
    [McpServerTool(Name = "verify_iban", ReadOnly = true, Idempotent = true)]
    [Description(
        "Verifies an IBAN locally using country length rules and MOD-97, then attempts to resolve the BIC from a local lookup table and external provider if necessary as a fallback."
    )]
    public async Task<IbanVerifyResponse> VerifyIban(
        [Description("The IBAN to verify, for example 'DE75 5121 0800 1245 1261 99'.")]
            IbanVerifyRequest request
    )
    {
        await _requestValidationService.ValidateAndThrowAsync(request);

        return await _ibanService.VerifyAsync(request.Iban);
    }
}
