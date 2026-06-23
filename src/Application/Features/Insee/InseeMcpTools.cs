using System.ComponentModel;
using CompanyInfo.Api.Shared.Validation;
using ModelContextProtocol.Server;

namespace CompanyInfo.Api.Application.Features.Insee;

/// <summary>
/// MCP tools for INSEE establishment lookup.
/// </summary>
[McpServerToolType]
public class InseeMcpTools
{
    private readonly IInseeService _inseeService;
    private readonly IRequestValidationService _requestValidationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="InseeMcpTools"/> class.
    /// </summary>
    /// <param name="inseeService">The INSEE service.</param>
    /// <param name="requestValidationService">The shared request validation service.</param>
    public InseeMcpTools(
        IInseeService inseeService,
        IRequestValidationService requestValidationService
    )
    {
        _inseeService = inseeService;
        _requestValidationService = requestValidationService;
    }

    /// <summary>
    /// Gets establishment information in France by SIRET number from the INSEE SIRENE API.
    /// </summary>
    /// <param name="request">The request containing the 14-digit SIRET number.</param>
    /// <returns>The establishment details.</returns>
    [McpServerTool(Name = "get_establishment_info", ReadOnly = true, Idempotent = true)]
    [Description(
        "Gets establishment information in France by SIRET number from the INSEE SIRENE API."
    )]
    public async Task<InseeEstablishmentResponse> GetEstablishmentInfo(
        [Description("The request containing the 14-digit SIRET number.")]
            InseeGetEstablishmentRequest request
    )
    {
        await _requestValidationService.ValidateAndThrowAsync(request);

        return await _inseeService.GetEstablishmentAsync(request.GetNormalizedSiret());
    }
}
