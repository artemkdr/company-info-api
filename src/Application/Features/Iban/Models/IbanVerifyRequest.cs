using System.ComponentModel;

namespace CompanyInfo.Api.Application.Features.Iban;

/// <summary>
/// Shared request model for IBAN verification and local BIC resolution.
/// </summary>
public class IbanVerifyRequest
{
    /// <summary>
    /// The IBAN to verify.
    /// </summary>
    [Description(
        "The IBAN to verify (spaces and dashes are ignored, for example 'DE75 5121 0800 1245 1261 99')."
    )]
    public string Iban { get; set; } = string.Empty;
}
