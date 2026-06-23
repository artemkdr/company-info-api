using System.ComponentModel;
using CompanyInfo.Api.Shared.Validation;
using ModelContextProtocol.Server;

namespace CompanyInfo.Api.Application.Features.Vies;

/// <summary>
/// MCP tools for VIES VAT number validation.
/// </summary>
[McpServerToolType]
public class ViesMcpTools
{
    private readonly IViesService _viesService;
    private readonly IRequestValidationService _requestValidationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ViesMcpTools"/> class.
    /// </summary>
    /// <param name="viesService">The VIES validation service.</param>
    /// <param name="requestValidationService">The shared request validation service.</param>
    public ViesMcpTools(
        IViesService viesService,
        IRequestValidationService requestValidationService
    )
    {
        _viesService = viesService;
        _requestValidationService = requestValidationService;
    }

    /// <summary>
    /// Validates the format of an EU VAT number locally, without calling any external service.
    /// This tool always responds immediately and never fails due to external unavailability.
    /// </summary>
    /// <param name="request">The request containing the VAT number to validate.</param>
    /// <returns>The format validation result.</returns>
    [McpServerTool(Name = "validate_vat_format", ReadOnly = true, Idempotent = true)]
    [Description(
        "Validates the format of an EU VAT number locally, without calling any external service. Always responds immediately and never fails due to external unavailability."
    )]
    public async Task<ViesFormatValidationResponse> ValidateVatFormat(
        [Description("The EU VAT number to validate (e.g., 'FR12345678901').")]
            ViesValidateFormatRequest request
    )
    {
        await _requestValidationService.ValidateAndThrowAsync(request);

        return _viesService.ValidateFormat(request.VatNumber);
    }

    /// <summary>
    /// Checks whether an EU VAT number is currently active in the VIES system.
    /// Calls the external EU VIES service with retry logic for transient errors.
    /// Results are cached to reduce load on the external service.
    /// </summary>
    /// <param name="request">The request containing the VAT number to check.</param>
    /// <returns>The active-check result.</returns>
    [McpServerTool(Name = "check_vat_active", ReadOnly = true, Idempotent = true)]
    [Description(
        "Checks whether an EU VAT number is currently active in the VIES system. Calls the external EU VIES service with retry logic for transient errors. Results are cached."
    )]
    public async Task<ViesValidationResponse> CheckVatActive(
        [Description("The EU VAT number to check for active status (e.g., 'FR12345678901').")]
            ViesCheckActiveRequest request
    )
    {
        await _requestValidationService.ValidateAndThrowAsync(request);

        return await _viesService.CheckActiveAsync(request.VatNumber);
    }
}
