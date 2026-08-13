using System.Text.RegularExpressions;
using FluentValidation;

namespace CompanyInfo.Api.Application.Features.ChIde;

/// <summary>
/// Validator for <see cref="ChIdeGetByUidRequest"/>.
/// </summary>
public partial class ChIdeGetByUidRequestValidator : AbstractValidator<ChIdeGetByUidRequest>
{
    /// <summary>
    /// Regex pattern for Swiss UID numbers: "CHE" prefix followed by 9 digits,
    /// optionally separated by dots and hyphens (e.g., "CHE-123.456.789" or "CHE123456789").
    /// </summary>
    [GeneratedRegex("^CHE[-.]?[0-9]{3}[-.]?[0-9]{3}[-.]?[0-9]{3}$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex UidFormatRegex();

    /// <summary>
    /// Initializes a new instance of the <see cref="ChIdeGetByUidRequestValidator"/> class.
    /// </summary>
    public ChIdeGetByUidRequestValidator()
    {
        RuleFor(request => request.Uid)
            .Must(uid => !string.IsNullOrWhiteSpace(uid))
            .WithMessage("UID is required.")
            .Must(uid => UidFormatRegex().IsMatch(uid))
            .WithMessage(
                "UID must be a valid Swiss UID number starting with 'CHE' followed by 9 digits (e.g., 'CHE-123.456.789' or 'CHE123456789')."
            );
    }
}
