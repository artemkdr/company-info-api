using System.ComponentModel;
using CompanyInfo.Api.Shared.Validation;
using ModelContextProtocol.Server;

namespace CompanyInfo.Api.Application.Features.ChIde;

/// <summary>
/// MCP tools for CH IDE (Swiss UID) lookups and validation.
/// </summary>
[McpServerToolType]
public class ChIdeMcpTools
{
    private readonly IChIdeService _chIdeService;
    private readonly IRequestValidationService _requestValidationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChIdeMcpTools"/> class.
    /// </summary>
    /// <param name="chIdeService">The CH IDE service.</param>
    /// <param name="requestValidationService">The shared request validation service.</param>
    public ChIdeMcpTools(
        IChIdeService chIdeService,
        IRequestValidationService requestValidationService
    )
    {
        _chIdeService = chIdeService;
        _requestValidationService = requestValidationService;
    }

    /// <summary>
    /// Looks up a Swiss company by its UID number via the CH IDE (UID-WSE) API.
    /// Results are cached to reduce load on the external service.
    /// </summary>
    /// <param name="request">The request containing the UID number to look up.</param>
    /// <returns>The company information.</returns>
    [McpServerTool(Name = "get_ch_ide_by_uid", ReadOnly = true, Idempotent = true)]
    [Description(
        "Looks up a Swiss company by its UID number via the CH IDE (UID-WSE) API. Results are cached to reduce load."
    )]
    public async Task<ChIdeGetByUidResponse> GetChIdeByUid(
        [Description(
            "The Swiss UID number to look up (e.g., 'CHE-123.456.789' or 'CHE123456789')."
        )]
            ChIdeGetByUidRequest request
    )
    {
        await _requestValidationService.ValidateAndThrowAsync(request);

        return await _chIdeService.GetByUidAsync(request.Uid);
    }

    /// <summary>
    /// Validates whether a Swiss UID number is currently registered and active in the CH IDE system.
    /// Results are cached to reduce load on the external service.
    /// </summary>
    /// <param name="request">The request containing the UID number to validate.</param>
    /// <returns>The validation result.</returns>
    [McpServerTool(Name = "validate_ch_ide_uid", ReadOnly = true, Idempotent = true)]
    [Description(
        "Validates whether a Swiss UID number is currently registered and active in the CH IDE system. Results are cached."
    )]
    public async Task<ChIdeValidateUidResponse> ValidateChIdeUid(
        [Description(
            "The Swiss UID number to validate (e.g., 'CHE-123.456.789' or 'CHE123456789')."
        )]
            ChIdeValidateUidRequest request
    )
    {
        await _requestValidationService.ValidateAndThrowAsync(request);

        return await _chIdeService.ValidateUidAsync(request.Uid);
    }
}
