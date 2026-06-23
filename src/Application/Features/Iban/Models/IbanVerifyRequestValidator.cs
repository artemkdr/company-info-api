using FluentValidation;

namespace CompanyInfo.Api.Application.Features.Iban;

/// <summary>
/// Validator for <see cref="IbanVerifyRequest"/>.
/// </summary>
public class IbanVerifyRequestValidator : AbstractValidator<IbanVerifyRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IbanVerifyRequestValidator"/> class.
    /// </summary>
    public IbanVerifyRequestValidator()
    {
        RuleFor(request => request.Iban)
            .Must(iban => !string.IsNullOrWhiteSpace(iban))
            .WithMessage("IBAN is required.");
    }
}
