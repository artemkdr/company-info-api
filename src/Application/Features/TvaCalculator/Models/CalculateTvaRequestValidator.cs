using FluentValidation;

namespace CompanyInfo.Api.Application.Features.TvaCalculator;

/// <summary>
/// Validator for <see cref="CalculateTvaRequest"/>.
/// </summary>
public class CalculateTvaRequestValidator : AbstractValidator<CalculateTvaRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CalculateTvaRequestValidator"/> class.
    /// </summary>
    public CalculateTvaRequestValidator()
    {
        RuleFor(request => request.Siren)
            .Must(siren => !string.IsNullOrWhiteSpace(siren))
            .WithMessage("SIREN number is required.")
            // must be 9 or 14 digits
            .Matches(@"^\d{9}(\d{5})?$")
            .WithMessage("SIREN number must be exactly 9 or 14 digits.");
    }
}
