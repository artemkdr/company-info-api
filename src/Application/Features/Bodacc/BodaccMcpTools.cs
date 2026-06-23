using System.ComponentModel;
using CompanyInfo.Api.Shared.Validation;
using ModelContextProtocol.Server;

namespace CompanyInfo.Api.Application.Features.Bodacc;

/// <summary>
/// MCP tools for BODACC searches.
/// </summary>
[McpServerToolType]
public class BodaccMcpTools
{
    private readonly IBodaccService _bodaccService;
    private readonly IRequestValidationService _requestValidationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="BodaccMcpTools"/> class.
    /// </summary>
    /// <param name="bodaccService">The BODACC service.</param>
    /// <param name="requestValidationService">The shared request validation service.</param>
    public BodaccMcpTools(
        IBodaccService bodaccService,
        IRequestValidationService requestValidationService
    )
    {
        _bodaccService = bodaccService;
        _requestValidationService = requestValidationService;
    }

    /// <summary>
    /// Searches BODACC records by SIREN or SIRET number to determine
    /// if a French company is in liquidation or has been dissolved.
    /// </summary>
    /// <param name="request">The request containing the SIREN (9 digits) or SIRET (14 digits) number.</param>
    /// <returns>The search results including liquidation status.</returns>
    [McpServerTool(Name = "search_bodacc_records", ReadOnly = true, Idempotent = true)]
    [Description(
        "Searches BODACC records by SIREN or SIRET number to determine if a French company is in liquidation or has been dissolved."
    )]
    public async Task<BodaccSearchResponse> SearchBodaccRecords(
        [Description("The SIREN (9 digits) or SIRET (14 digits) number.")]
            BodaccSearchRequest request
    )
    {
        await _requestValidationService.ValidateAndThrowAsync(request);

        return await _bodaccService.SearchAsync(request.GetNormalizedRegistrationNumber());
    }
}
