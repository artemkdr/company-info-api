using FluentValidation;

namespace CompanyInfo.Api.Application.Features.ChIde;

/// <summary>
/// Validator for <see cref="ChIdeGetByUidRequest"/>.
/// </summary>
public class ChIdeGetByUidRequestValidator : AbstractValidator<ChIdeGetByUidRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ChIdeGetByUidRequestValidator"/> class.
    /// </summary>
    public ChIdeGetByUidRequestValidator()
    {
        RuleFor(request => request.Uid)
            .Must(uid => !string.IsNullOrWhiteSpace(uid))
            .WithMessage("UID is required.");
    }
}
