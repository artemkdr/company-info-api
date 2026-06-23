using Asp.Versioning;
using CompanyInfo.Api.Shared.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CompanyInfo.Api.Application.Features.Insee;

/// <summary>
/// Controller for looking up French establishments via the INSEE SIRENE API.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/insee")]
[Authorize]
[Produces("application/json", "application/xml")]
public class InseeController : ControllerBase
{
    private readonly IInseeService _inseeService;
    private readonly IRequestValidationService _requestValidationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="InseeController"/> class.
    /// </summary>
    /// <param name="inseeService">The INSEE service.</param>
    /// <param name="requestValidationService">The shared request validation service.</param>
    public InseeController(
        IInseeService inseeService,
        IRequestValidationService requestValidationService
    )
    {
        _inseeService = inseeService;
        _requestValidationService = requestValidationService;
    }

    /// <summary>
    /// Gets establishment information by SIRET number from the INSEE SIRENE API.
    /// </summary>
    /// <param name="request">The request containing the 14-digit SIRET number - French business identifier</param>
    /// <returns>The establishment details.</returns>
    /// <remarks>
    /// Always returns HTTP 200 OK. Check the <c>errorMessage</c> field in the response body to determine
    /// whether the external INSEE service accepted the request. A non-null <c>errorMessage</c> means
    /// the external service returned an error; HTTP 4xx is only returned for malformed input.
    /// </remarks>
    /// <response code="200">Returns the establishment information.</response>
    /// <response code="400">If the SIRET number is missing or invalid.</response>
    [HttpGet("establishments/{Siret}")]
    [ProducesResponseType(typeof(InseeEstablishmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetEstablishment(
        [FromRoute] InseeGetEstablishmentRequest request
    )
    {
        await _requestValidationService.ValidateAndThrowAsync(request);

        var result = await _inseeService.GetEstablishmentAsync(request.GetNormalizedSiret());
        return Ok(result);
    }
}
