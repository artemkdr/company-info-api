using Asp.Versioning;
using CompanyInfo.Api.Shared.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CompanyInfo.Api.Application.Features.Iban;

/// <summary>
/// Controller for local IBAN verification and BIC resolution.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/iban")]
[Authorize]
[Produces("application/json", "application/xml")]
public class IbanController : ControllerBase
{
    private readonly IIbanService _ibanService;
    private readonly IRequestValidationService _requestValidationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="IbanController"/> class.
    /// </summary>
    /// <param name="ibanService">The IBAN verification service.</param>
    /// <param name="requestValidationService">The shared request validation service.</param>
    public IbanController(
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
    /// <remarks>
    /// Always returns HTTP 200 OK. Check the <c>errorMessage</c> field in the response body to determine
    /// whether the IBAN was accepted. A non-null <c>errorMessage</c> means the IBAN is invalid
    /// (malformed, bad checksum, unsupported country, etc.); HTTP 4xx is only returned when the IBAN
    /// field is missing entirely.
    /// </remarks>
    /// <response code="200">Returns the verification result.</response>
    /// <response code="400">If the IBAN is missing.</response>
    [HttpGet("verify")]
    [ProducesResponseType(typeof(IbanVerifyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Verify([FromQuery] IbanVerifyRequest request)
    {
        await _requestValidationService.ValidateAndThrowAsync(request);

        var result = await _ibanService.VerifyAsync(request.Iban);
        return Ok(result);
    }
}
