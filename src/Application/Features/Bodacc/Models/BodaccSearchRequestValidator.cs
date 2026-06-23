using FluentValidation;

namespace CompanyInfo.Api.Application.Features.Bodacc;

/// <summary>
/// Validator for <see cref="BodaccSearchRequest"/>.
/// </summary>
public class BodaccSearchRequestValidator : AbstractValidator<BodaccSearchRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BodaccSearchRequestValidator"/> class.
    /// </summary>
    public BodaccSearchRequestValidator()
    {
        RuleFor(request => request.RegistrationNumber)
            .Must(registrationNumber => !string.IsNullOrWhiteSpace(registrationNumber))
            .WithMessage("Registration number (SIREN or SIRET) is required.");

        RuleFor(request => request)
            .Must(request =>
            {
                var normalized = request.GetNormalizedRegistrationNumber();
                return normalized.Length == 9 || normalized.Length == 14;
            })
            .WithMessage("Registration number must be a 9-digit SIREN or 14-digit SIRET number.");
    }
}
