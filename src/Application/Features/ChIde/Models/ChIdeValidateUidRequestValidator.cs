using FluentValidation;

namespace CompanyInfo.Api.Application.Features.ChIde;

/// <summary>
/// Validator for <see cref="ChIdeValidateUidRequest"/>.
/// </summary>
public class ChIdeValidateUidRequestValidator : AbstractValidator<ChIdeValidateUidRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ChIdeValidateUidRequestValidator"/> class.
    /// </summary>
    public ChIdeValidateUidRequestValidator()
    {
        RuleFor(request => request.Uid)
            .Must(uid => !string.IsNullOrWhiteSpace(uid))
            .WithMessage("UID is required.");
    }
}
