using FluentValidation;

namespace CompanyInfo.Api.Application.Features.Insee;

/// <summary>
/// Validator for <see cref="InseeGetEstablishmentRequest"/>.
/// </summary>
public class InseeGetEstablishmentRequestValidator : AbstractValidator<InseeGetEstablishmentRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InseeGetEstablishmentRequestValidator"/> class.
    /// </summary>
    public InseeGetEstablishmentRequestValidator()
    {
        RuleFor(request => request.Siret)
            .Must(siret => !string.IsNullOrWhiteSpace(siret))
            .WithMessage("SIRET number is required.");

        RuleFor(request => request)
            .Must(request => request.GetNormalizedSiret().Length == 14)
            .WithMessage("SIRET number must be exactly 14 digits.");
    }
}
