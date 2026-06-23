using Asp.Versioning;
using CompanyInfo.Api.Shared.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CompanyInfo.Api.Application.Features.ChIde;

/// <summary>
/// Controller for looking up and validating Swiss UID (IDE) numbers
/// via the CH IDE (UID-WSE) public API.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/ch-ide")]
[Authorize]
[Produces("application/json", "application/xml")]
public class ChIdeController : ControllerBase
{
    private readonly IChIdeService _chIdeService;
    private readonly IRequestValidationService _requestValidationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChIdeController"/> class.
    /// </summary>
    /// <param name="chIdeService">The CH IDE service.</param>
    /// <param name="requestValidationService">The shared request validation service.</param>
    public ChIdeController(
        IChIdeService chIdeService,
        IRequestValidationService requestValidationService
    )
    {
        _chIdeService = chIdeService;
        _requestValidationService = requestValidationService;
    }

    /// <summary>
    /// Looks up a Swiss company by its UID number.
    /// </summary>
    /// <param name="request">The request containing the UID number (e.g., "CHE-123.456.789" or "CHE123456789").</param>
    /// <returns>The company details.</returns>
    /// <remarks>
    /// Always returns HTTP 200 OK. Check the <c>errorMessage</c> field in the response body to determine
    /// whether the external CH IDE service accepted the request. A non-null <c>errorMessage</c> means
    /// the external service returned an error; HTTP 4xx is only returned for malformed input.
    /// </remarks>
    /// <response code="200">Returns the company information.</response>
    /// <response code="400">If the UID is missing.</response>
    [HttpGet("{Uid}")]
    [ProducesResponseType(typeof(ChIdeGetByUidResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetByUid([FromRoute] ChIdeGetByUidRequest request)
    {
        await _requestValidationService.ValidateAndThrowAsync(request);

        var result = await _chIdeService.GetByUidAsync(request.Uid);
        return Ok(result);
    }

    /// <summary>
    /// Validates whether a Swiss UID number is registered and active.
    /// </summary>
    /// <param name="request">The request containing the UID number to validate.</param>
    /// <returns>The validation result.</returns>
    /// <remarks>
    /// Always returns HTTP 200 OK. Check the <c>errorMessage</c> field in the response body to determine
    /// whether the external CH IDE service accepted the request. A non-null <c>errorMessage</c> means
    /// the external service returned an error; HTTP 4xx is only returned for malformed input.
    /// </remarks>
    /// <response code="200">Returns the validation result.</response>
    /// <response code="400">If the UID is missing.</response>
    [HttpGet("{Uid}/validate")]
    [ProducesResponseType(typeof(ChIdeValidateUidResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ValidateUid([FromRoute] ChIdeValidateUidRequest request)
    {
        await _requestValidationService.ValidateAndThrowAsync(request);

        var result = await _chIdeService.ValidateUidAsync(request.Uid);
        return Ok(result);
    }
}
