using Asp.Versioning;
using CompanyInfo.Api.Shared.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CompanyInfo.Api.Application.Features.TvaCalculator;

/// <summary>
/// Controller for calculating French TVA (VAT) intra-community numbers from SIREN numbers.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/tva-calculator")]
[Authorize]
[Produces("application/json", "application/xml")]
public class TvaCalculatorController : ControllerBase
{
    private readonly ITvaCalculatorService _tvaCalculatorService;
    private readonly IRequestValidationService _requestValidationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="TvaCalculatorController"/> class.
    /// </summary>
    /// <param name="tvaCalculatorService">The TVA calculator service.</param>
    /// <param name="requestValidationService">The shared request validation service.</param>
    public TvaCalculatorController(
        ITvaCalculatorService tvaCalculatorService,
        IRequestValidationService requestValidationService
    )
    {
        _tvaCalculatorService = tvaCalculatorService;
        _requestValidationService = requestValidationService;
    }

    /// <summary>
    /// Calculates the French TVA (VAT) intra-community number from a SIREN number.
    /// </summary>
    /// <param name="request">The request containing the 9-digit SIREN number.</param>
    /// <returns>The calculated TVA number.</returns>
    /// <remarks>
    /// Always returns HTTP 200 OK. Check <c>isValid</c> and <c>errorMessage</c> in the response body.
    /// A non-null <c>errorMessage</c> describes why the SIREN could not be processed;
    /// HTTP 4xx is only returned when the SIREN is missing or structurally invalid per the request validator.
    /// </remarks>
    /// <response code="200">Returns the calculated TVA number.</response>
    /// <response code="400">If the SIREN number is missing or invalid.</response>
    [HttpGet]
    [ProducesResponseType(typeof(CalculateTvaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Calculate([FromQuery] CalculateTvaRequest request)
    {
        await _requestValidationService.ValidateAndThrowAsync(request);

        var result = _tvaCalculatorService.Calculate(request.Siren);
        return Ok(result);
    }
}
