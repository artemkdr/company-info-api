using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;

namespace CompanyInfo.Api.Application.Features.ChIde;

/// <summary>
/// Shared request model for CH IDE UID lookup.
/// </summary>
public class ChIdeGetByUidRequest
{
    /// <summary>
    /// The Swiss UID number to look up (e.g., "CHE-123.456.789" or "CHE123456789").
    /// </summary>
    [FromRoute(Name = "Uid")]
    [Description("The Swiss UID number to look up (e.g., 'CHE-123.456.789' or 'CHE123456789').")]
    public string Uid { get; set; } = string.Empty;
}
