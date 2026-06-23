using FluentValidation;

namespace CompanyInfo.Api.Application.Features.Vies;

/// <summary>
/// Validator for <see cref="ViesValidateFormatRequest"/>.
/// </summary>
public class ViesValidateFormatRequestValidator : AbstractValidator<ViesValidateFormatRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ViesValidateFormatRequestValidator"/> class.
    /// </summary>
    public ViesValidateFormatRequestValidator()
    {
        RuleFor(request => request.VatNumber)
            .Must(vatNumber => !string.IsNullOrWhiteSpace(vatNumber))
            .WithMessage("VAT number is required.");
    }
}
