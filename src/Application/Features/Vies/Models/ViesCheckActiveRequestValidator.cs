using FluentValidation;

namespace CompanyInfo.Api.Application.Features.Vies;

/// <summary>
/// Validator for <see cref="ViesCheckActiveRequest"/>.
/// </summary>
public class ViesCheckActiveRequestValidator : AbstractValidator<ViesCheckActiveRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ViesCheckActiveRequestValidator"/> class.
    /// </summary>
    public ViesCheckActiveRequestValidator()
    {
        RuleFor(request => request.VatNumber)
            .Must(vatNumber => !string.IsNullOrWhiteSpace(vatNumber))
            .WithMessage("VAT number is required.");
    }
}
