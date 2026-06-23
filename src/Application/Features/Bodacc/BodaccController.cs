using Asp.Versioning;
using CompanyInfo.Api.Shared.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CompanyInfo.Api.Application.Features.Bodacc;

/// <summary>
/// Controller for searching BODACC records to check French company bankruptcy/dissolution status.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/bodacc")]
[Authorize]
[Produces("application/json", "application/xml")]
public class BodaccController : ControllerBase
{
    private readonly IBodaccService _bodaccService;
    private readonly IRequestValidationService _requestValidationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="BodaccController"/> class.
    /// </summary>
    /// <param name="bodaccService">The BODACC service.</param>
    /// <param name="requestValidationService">The shared request validation service.</param>
    public BodaccController(
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
    /// <remarks>
    /// Always returns HTTP 200 OK. Check the <c>errorMessage</c> field in the response body to determine
    /// whether the external BODACC service accepted the request. A non-null <c>errorMessage</c> means
    /// the external service returned an error; HTTP 4xx is only returned for malformed input.
    /// </remarks>
    /// <response code="200">Returns the BODACC search results.</response>
    /// <response code="400">If the registration number is missing or invalid.</response>
    [HttpGet("search")]
    [ProducesResponseType(typeof(BodaccSearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Search([FromQuery] BodaccSearchRequest request)
    {
        await _requestValidationService.ValidateAndThrowAsync(request);

        var result = await _bodaccService.SearchAsync(request.GetNormalizedRegistrationNumber());
        return Ok(result);
    }
}
