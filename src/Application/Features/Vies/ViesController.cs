using Asp.Versioning;
using CompanyInfo.Api.Shared.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CompanyInfo.Api.Application.Features.Vies;

/// <summary>
/// Controller for validating EU VAT numbers via the VIES system.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/vies")]
[Authorize]
[Produces("application/json", "application/xml")]
public class ViesController : ControllerBase
{
    private readonly IViesService _viesService;
    private readonly IRequestValidationService _requestValidationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ViesController"/> class.
    /// </summary>
    /// <param name="viesService">The VIES validation service.</param>
    /// <param name="requestValidationService">The shared request validation service.</param>
    public ViesController(
        IViesService viesService,
        IRequestValidationService requestValidationService
    )
    {
        _viesService = viesService;
        _requestValidationService = requestValidationService;
    }

    /// <summary>
    /// Validates the format of an EU VAT number locally, without calling any external service.
    /// This endpoint always responds immediately and never fails due to external unavailability.
    /// </summary>
    /// <param name="request">The request containing the VAT number to validate (e.g., "FR12345678901").</param>
    /// <returns>The format validation result.</returns>
    /// <response code="200">Returns the format validation result.</response>
    /// <response code="400">If the VAT number is missing.</response>
    [HttpGet("validate")]
    [ProducesResponseType(typeof(ViesFormatValidationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Validate([FromQuery] ViesValidateFormatRequest request)
    {
        await _requestValidationService.ValidateAndThrowAsync(request);

        var result = _viesService.ValidateFormat(request.VatNumber);
        return Ok(result);
    }

    /// <summary>
    /// Checks whether an EU VAT number is currently active in the VIES system.
    /// Calls the external EU VIES service with retry logic for transient errors.
    /// Results are cached to reduce load on the external service.
    /// </summary>
    /// <param name="request">The request containing the VAT number to check (e.g., "FR12345678901").</param>
    /// <returns>The active-check result.</returns>
    /// <remarks>
    /// Always returns HTTP 200 OK. Check the <c>errorMessage</c> field in the response body to determine
    /// whether the external EU VIES service accepted the request. A non-null <c>errorMessage</c> means
    /// the external service returned an error or the VAT number is inactive; HTTP 4xx is only returned
    /// for malformed input.
    /// </remarks>
    /// <response code="200">Returns whether the VAT number is active.</response>
    /// <response code="400">If the VAT number is missing.</response>
    [HttpGet("check-active")]
    [ProducesResponseType(typeof(ViesValidationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CheckActive([FromQuery] ViesCheckActiveRequest request)
    {
        await _requestValidationService.ValidateAndThrowAsync(request);

        var result = await _viesService.CheckActiveAsync(request.VatNumber);
        return Ok(result);
    }
}
