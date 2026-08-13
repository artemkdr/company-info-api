using System.Text.RegularExpressions;
using FluentValidation;

namespace CompanyInfo.Api.Application.Features.Vies;

/// <summary>
/// Validator for <see cref="ViesCheckActiveRequest"/>.
/// </summary>
public partial class ViesCheckActiveRequestValidator : AbstractValidator<ViesCheckActiveRequest>
{
    /// <summary>
    /// Regex pattern for EU VAT numbers: 2-letter country code followed by 2-12 alphanumeric characters.
    /// </summary>
    [GeneratedRegex("^[A-Za-z]{2}[A-Za-z0-9]{2,12}$", RegexOptions.Compiled)]
    private static partial Regex VatNumberFormatRegex();

    /// <summary>
    /// Initializes a new instance of the <see cref="ViesCheckActiveRequestValidator"/> class.
    /// </summary>
    public ViesCheckActiveRequestValidator()
    {
        RuleFor(request => request.VatNumber)
            .Must(vatNumber => !string.IsNullOrWhiteSpace(vatNumber))
            .WithMessage("VAT number is required.")
            .Must(vatNumber => VatNumberFormatRegex().IsMatch(vatNumber))
            .WithMessage(
                "VAT number must start with a 2-letter country code followed by 2-12 alphanumeric characters (e.g., 'FR12345678901')."
            );
    }
}
