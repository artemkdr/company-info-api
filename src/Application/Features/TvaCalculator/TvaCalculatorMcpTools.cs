using System.ComponentModel;
using CompanyInfo.Api.Shared.Validation;
using ModelContextProtocol.Server;

namespace CompanyInfo.Api.Application.Features.TvaCalculator;

/// <summary>
/// MCP tools for French TVA (VAT) calculation.
/// </summary>
[McpServerToolType]
public class TvaCalculatorMcpTools
{
    private readonly ITvaCalculatorService _tvaCalculatorService;
    private readonly IRequestValidationService _requestValidationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="TvaCalculatorMcpTools"/> class.
    /// </summary>
    /// <param name="tvaCalculatorService">The TVA calculator service.</param>
    /// <param name="requestValidationService">The shared request validation service.</param>
    public TvaCalculatorMcpTools(
        ITvaCalculatorService tvaCalculatorService,
        IRequestValidationService requestValidationService
    )
    {
        _tvaCalculatorService = tvaCalculatorService;
        _requestValidationService = requestValidationService;
    }

    /// <summary>
    /// Calculates the French TVA (VAT) intra-community number from a SIREN number.
    /// Formula: FR + ((12 + 3 * (siren % 97)) % 97).PadLeft(2, '0') + siren
    /// </summary>
    /// <param name="request">The request containing the SIREN number.</param>
    /// <returns>The TVA calculation result.</returns>
    [McpServerTool(Name = "calculate_french_tva", ReadOnly = true, Idempotent = true)]
    [Description(
        "Calculates the French TVA (VAT) intra-community number from a SIREN number. Formula: FR + ((12 + 3 * (siren % 97)) % 97).PadLeft(2, '0') + siren"
    )]
    public async Task<CalculateTvaResponse> CalculateFrenchTva(
        [Description("The 9-digit French SIREN number (e.g., '123456789').")]
            CalculateTvaRequest request
    )
    {
        await _requestValidationService.ValidateAndThrowAsync(request);

        return _tvaCalculatorService.Calculate(request.Siren);
    }
}
